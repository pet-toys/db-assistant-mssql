using System;
using AwesomeAssertions;
using PetToys.DbAssistant.Mssql.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

public sealed class PropertyAccessorTest
{
    [Theory]
    [MemberData(nameof(DataGenerator.NullableContextCases), MemberType = typeof(DataGenerator))]
    public void Ctor_NullableContext_ResolvesMetadataAndValue(AccessorCase<NullableEnabledEntity> @case)
        => AssertCase(@case, @case.Entity);

    [Theory]
    [MemberData(nameof(DataGenerator.NotNullableContextCases), MemberType = typeof(DataGenerator))]
    public void Ctor_NotNullableContext_ResolvesMetadataAndValue(AccessorCase<NullableDisabledEntity> @case)
        => AssertCase(@case, @case.Entity);

    [Theory]
    [MemberData(nameof(DataGenerator.DateTimeFamilyCases), MemberType = typeof(DataGenerator))]
    public void Ctor_DateTimeFamily_ResolvesMetadataAndValue(AccessorCase<DateTimeEntity> @case)
        => AssertCase(@case, @case.Entity);

    [Theory]
    [InlineData("custom", "[custom]")]
    [InlineData("custom col", "[custom col]")]
    [InlineData("[already]", "[already]")]
    [InlineData("weird]name", "[weird]]name]")]
    public void Ctor_ExplicitColumnName_IsQuoted(string columnName, string expected)
    {
        var accessor = new PropertyAccessor<NullableEnabledEntity, int>(e => e.Int0, columnName);

        accessor.ColumnName.Should().Be(expected);
        accessor.PropertyName.Should().Be(nameof(NullableEnabledEntity.Int0));
    }

    [Fact]
    public void Ctor_BinaryExpression_ThrowsArgumentException()
    {
        var act = () => new PropertyAccessor<NullableEnabledEntity, int>(e => e.Int0 + 1);

        act.Should().Throw<ArgumentException>().WithMessage("*property expression*");
    }

    [Fact]
    public void Ctor_ConstantExpression_ThrowsArgumentException()
    {
        var act = () => new PropertyAccessor<NullableEnabledEntity, int>(_ => 42);

        act.Should().Throw<ArgumentException>().WithMessage("*property expression*");
    }

    [Fact]
    public void Ctor_MethodCallExpression_ThrowsArgumentException()
    {
        var act = () => new PropertyAccessor<NullableEnabledEntity, string>(e => e.Str0.ToUpperInvariant());

        act.Should().Throw<ArgumentException>().WithMessage("*property expression*");
    }

    [Fact]
    public void Ctor_UnsupportedPropertyType_ThrowsInvalidOperationException()
    {
        var act = () => new PropertyAccessor<UnsupportedEntity, Uri>(e => e.Link);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not supported*")
            .WithMessage($"*{nameof(Uri)}*");
    }

    /// <summary>
    /// The nullable case deliberately uses a nullable value type rather than a nullable reference
    /// type.
    /// </summary>
    /// <remarks>
    /// <see cref="PropertyAccessor{TEntity,TProperty}"/> resolves a nullable value type through
    /// <see cref="Nullable.GetUnderlyingType"/> and reduces it before consulting the whitelist,
    /// whereas a nullable reference type is resolved through <c>NullabilityInfoContext</c> and its
    /// type is not reduced at all. <c>Uri?</c> would therefore travel a different branch and leave
    /// the reduction this test exists for uncovered.
    /// </remarks>
    [Fact]
    public void Ctor_UnsupportedNullablePropertyType_ThrowsInvalidOperationException()
    {
        var act = () => new PropertyAccessor<UnsupportedEntity, LinkKind?>(e => e.NullableKind);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not supported*")
            .WithMessage($"*{nameof(LinkKind)}*");
    }

    private static void AssertCase<TEntity>(AccessorCase<TEntity> @case, TEntity entity)
        where TEntity : class
    {
        @case.Accessor.PropertyName.Should().Be(@case.PropertyName);
        @case.Accessor.ColumnName.Should().Be(@case.ColumnName);
        @case.Accessor.ClrType.Should().Be(@case.ClrType);
        @case.Accessor.EffectiveType.Should().Be(@case.EffectiveType);
        @case.Accessor.IsNullable.Should().Be(@case.IsNullable);
        @case.Accessor.GetValue(entity).Should().BeEquivalentTo(@case.Value);
    }

    /// <summary>
    /// Two properties of types the whitelist does not admit, held here to prove the guard fires.
    /// </summary>
    /// <remarks>
    /// A <see cref="Uri"/> is a plausible thing to find on a real model and there is no SQL Server
    /// type for it, so admitting it would mean the library choosing a stringification policy of its
    /// own. An enum is rejected for a different reason: it has an underlying integral type, so
    /// mapping one is a decision about whether to write the number or the name rather than an
    /// omission, and that decision has not been taken.
    /// </remarks>
    private sealed class UnsupportedEntity
    {
        public Uri Link { get; init; } = new("https://example.invalid");

        public LinkKind? NullableKind { get; init; }
    }

    private enum LinkKind
    {
        Short,
    }
}
