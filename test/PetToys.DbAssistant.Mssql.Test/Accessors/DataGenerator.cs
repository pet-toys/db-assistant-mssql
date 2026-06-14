using System;
using System.Linq.Expressions;
using PetToys.DbAssistant.Mssql.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

/// <summary>
/// Theory data for <see cref="PropertyAccessorTest"/>. Each row asserts a
/// distinct behaviour; the previous true/false <c>referenceNullable</c> pairs
/// that produced identical results have been collapsed. <c>referenceNullable</c>
/// only changes the outcome for reference-typed properties in a
/// nullable-disabled context (where the nullability state is <c>Unknown</c>),
/// so only those keep both rows.
/// </summary>
public static class DataGenerator
{
    private static readonly DateTime SampleDate = new(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Cases for a nullable-enabled entity: <c>NullabilityInfoContext</c>
    /// always yields a definitive state, so <c>referenceNullable</c> is irrelevant.
    /// </summary>
    public static TheoryData<AccessorCase<NullableEnabledEntity>> NullableContextCases()
    {
        var data = new TheoryData<AccessorCase<NullableEnabledEntity>>();

        data.Add(Case("Int0 (non-null value type)",
            e => e.Int0, "Int0", "[Int0]", typeof(int), typeof(int), false,
            new NullableEnabledEntity { Int0 = 1 }, 1));

        data.Add(Case("Int1 (nullable value type)",
            e => e.Int1, "Int1", "[Int1]", typeof(int?), typeof(int), true,
            new NullableEnabledEntity { Int1 = 1 }, 1));

        data.Add(Case("Date0 (non-null DateTime)",
            e => e.Date0, "Date0", "[Date0]", typeof(DateTime), typeof(DateTime), false,
            new NullableEnabledEntity { Date0 = SampleDate }, SampleDate));

        data.Add(Case("Date1 (nullable DateTime)",
            e => e.Date1, "Date1", "[Date1]", typeof(DateTime?), typeof(DateTime), true,
            new NullableEnabledEntity { Date1 = SampleDate }, SampleDate));

        data.Add(Case("Str0 (non-null reference)",
            e => e.Str0, "Str0", "[Str0]", typeof(string), typeof(string), false,
            new NullableEnabledEntity { Str0 = "x" }, "x"));

        data.Add(Case("Str1 (nullable reference, value present)",
            e => e.Str1, "Str1", "[Str1]", typeof(string), typeof(string), true,
            new NullableEnabledEntity { Str1 = "y" }, "y"));

        data.Add(Case("Str1 (nullable reference, value null)",
            e => e.Str1, "Str1", "[Str1]", typeof(string), typeof(string), true,
            new NullableEnabledEntity { Str1 = null }, null));

        data.Add(Case("Arr0 (non-null byte[])",
            e => e.Arr0, "Arr0", "[Arr0]", typeof(byte[]), typeof(byte[]), false,
            new NullableEnabledEntity { Arr0 = [1, 2] }, new byte[] { 1, 2 }));

        data.Add(Case("Arr1 (nullable byte[], value null)",
            e => e.Arr1, "Arr1", "[Arr1]", typeof(byte[]), typeof(byte[]), true,
            new NullableEnabledEntity { Arr1 = null }, null));

        return data;
    }

    /// <summary>
    /// Cases for a nullable-disabled entity: reference-typed properties report an
    /// <c>Unknown</c> nullability state, so the accessor falls back to the
    /// <c>referenceNullable</c> flag — the only place where that flag matters.
    /// Value types remain context-independent.
    /// </summary>
    public static TheoryData<AccessorCase<NullableDisabledEntity>> NotNullableContextCases()
    {
        var data = new TheoryData<AccessorCase<NullableDisabledEntity>>();

        data.Add(Case("Int0 (value type, flag ignored)",
            e => e.Int0, "Int0", "[Int0]", typeof(int), typeof(int), false,
            new NullableDisabledEntity { Int0 = 1 }, 1, referenceNullable: true));

        data.Add(Case("Int1 (nullable value type, flag ignored)",
            e => e.Int1, "Int1", "[Int1]", typeof(int?), typeof(int), true,
            new NullableDisabledEntity { Int1 = 1 }, 1, referenceNullable: false));

        data.Add(Case("Str0 (unknown state, flag => nullable)",
            e => e.Str0, "Str0", "[Str0]", typeof(string), typeof(string), true,
            new NullableDisabledEntity { Str0 = "x" }, "x", referenceNullable: true));

        data.Add(Case("Str0 (unknown state, flag => not null)",
            e => e.Str0, "Str0", "[Str0]", typeof(string), typeof(string), false,
            new NullableDisabledEntity { Str0 = "x" }, "x", referenceNullable: false));

        data.Add(Case("Str1 (unknown state, flag => nullable, value null)",
            e => e.Str1, "Str1", "[Str1]", typeof(string), typeof(string), true,
            new NullableDisabledEntity { Str1 = null }, null, referenceNullable: true));

        data.Add(Case("Str1 (unknown state, flag => not null, value null)",
            e => e.Str1, "Str1", "[Str1]", typeof(string), typeof(string), false,
            new NullableDisabledEntity { Str1 = null }, null, referenceNullable: false));

        data.Add(Case("Arr0 (unknown state, flag => nullable)",
            e => e.Arr0, "Arr0", "[Arr0]", typeof(byte[]), typeof(byte[]), true,
            new NullableDisabledEntity { Arr0 = [1, 2] }, new byte[] { 1, 2 }, referenceNullable: true));

        data.Add(Case("Arr0 (unknown state, flag => not null)",
            e => e.Arr0, "Arr0", "[Arr0]", typeof(byte[]), typeof(byte[]), false,
            new NullableDisabledEntity { Arr0 = [1, 2] }, new byte[] { 1, 2 }, referenceNullable: false));

        return data;
    }

    private static AccessorCase<TEntity> Case<TEntity, TProperty>(
        string description,
        Expression<Func<TEntity, TProperty>> property,
        string propertyName,
        string columnName,
        Type clrType,
        Type effectiveType,
        bool isNullable,
        TEntity entity,
        object? value,
        bool referenceNullable = true)
        where TEntity : class =>
        new(
            description,
            new PropertyAccessor<TEntity, TProperty>(property, columnName: null, referenceNullable),
            propertyName,
            columnName,
            clrType,
            effectiveType,
            isNullable,
            entity,
            value);
}
