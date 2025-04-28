using System;
using FluentAssertions;
using PetToys.DbAssistant.Mssql.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

public sealed class PropertyAccessorTest
{
    [Theory]
    [MemberData(nameof(DataGenerator.DataGeneratorForNullableContext), MemberType = typeof(DataGenerator))]
    public void Ctor_NullableContext_PropertyTest(
        IPropertyAccessor<NullableEnabledEntity> accessor,
        string propertyName,
        string columnName,
        Type clrType,
        Type effectiveType,
        bool allowNull,
        NullableEnabledEntity entity,
        object? value)
    {
        accessor.PropertyName.Should().Be(propertyName);
        accessor.ColumnName.Should().Be(columnName);
        accessor.ClrType.Should().Be(clrType);
        accessor.EffectiveType.Should().Be(effectiveType);
        accessor.IsNullable.Should().Be(allowNull);
        accessor.GetValue(entity).Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(DataGenerator.DataGeneratorForNotNullableContext), MemberType = typeof(DataGenerator))]
    public void Ctor_NotNullableContext_PropertyTest(
        IPropertyAccessor<NullableDisabledEntity> accessor,
        string propertyName,
        string columnName,
        Type clrType,
        Type effectiveType,
        bool allowNull,
        NullableDisabledEntity entity,
        object? value)
    {
        accessor.PropertyName.Should().Be(propertyName);
        accessor.ColumnName.Should().Be(columnName);
        accessor.ClrType.Should().Be(clrType);
        accessor.EffectiveType.Should().Be(effectiveType);
        accessor.IsNullable.Should().Be(allowNull);
        accessor.GetValue(entity).Should().Be(value);
    }
}
