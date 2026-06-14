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
        var act = () => new PropertyAccessor<UnsupportedEntity, TimeSpan>(e => e.Span);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not supported*");
    }

    [Fact]
    public void Ctor_UnsupportedNullablePropertyType_ThrowsInvalidOperationException()
    {
        var act = () => new PropertyAccessor<UnsupportedEntity, TimeSpan?>(e => e.NullableSpan);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not supported*");
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

    private sealed class UnsupportedEntity
    {
        public TimeSpan Span { get; init; }

        public TimeSpan? NullableSpan { get; init; }
    }
}
