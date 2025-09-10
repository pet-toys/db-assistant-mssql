using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using PetToys.DbAssistant.Mssql.Accessors;
using PetToys.DbAssistant.Mssql.Extensions;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

public sealed class DataGenerator
{
    public static IEnumerable<object[]> DataGeneratorForNullableContext()
    {
        // Int0
        yield return
        [
            CreateAccessor<NullableEnabledEntity, int>(entity => entity.Int0, referenceNullable: true),
            nameof(NullableEnabledEntity.Int0),
            nameof(NullableEnabledEntity.Int0).QuoteName(),
            typeof(int),
            typeof(int),
            false,
            new NullableEnabledEntity { Int0 = 1 },
            1,
        ];

        yield return
        [
            CreateAccessor<NullableEnabledEntity, int>(entity => entity.Int0, referenceNullable: false),
            nameof(NullableEnabledEntity.Int0),
            nameof(NullableEnabledEntity.Int0).QuoteName(),
            typeof(int),
            typeof(int),
            false,
            new NullableEnabledEntity { Int0 = 1 },
            1,
        ];

        // Int1
        yield return
        [
            CreateAccessor<NullableEnabledEntity, int?>(entity => entity.Int1, referenceNullable: true),
            nameof(NullableEnabledEntity.Int1),
            nameof(NullableEnabledEntity.Int1).QuoteName(),
            typeof(int?),
            typeof(int),
            true,
            new NullableEnabledEntity { Int1 = 1 },
            1,
        ];

        yield return
        [
            CreateAccessor<NullableEnabledEntity, int?>(entity => entity.Int1, referenceNullable: false),
            nameof(NullableEnabledEntity.Int1),
            nameof(NullableEnabledEntity.Int1).QuoteName(),
            typeof(int?),
            typeof(int),
            true,
            new NullableEnabledEntity { Int1 = 1 },
            1,
        ];

        //Str0
        yield return
        [
            CreateAccessor<NullableEnabledEntity, string>(entity => entity.Str0, referenceNullable: true),
            nameof(NullableEnabledEntity.Str0),
            nameof(NullableEnabledEntity.Str0).QuoteName(),
            typeof(string),
            typeof(string),
            false,
            new NullableEnabledEntity { Str0 = string.Empty },
            string.Empty,
        ];

        yield return
        [
            CreateAccessor<NullableEnabledEntity, string>(entity => entity.Str0, referenceNullable: false),
            nameof(NullableEnabledEntity.Str0),
            nameof(NullableEnabledEntity.Str0).QuoteName(),
            typeof(string),
            typeof(string),
            false,
            new NullableEnabledEntity { Str0 = string.Empty },
            string.Empty,
        ];

        //Str1
        yield return
        [
            CreateAccessor<NullableEnabledEntity, string?>(entity => entity.Str1, referenceNullable: true),
            nameof(NullableEnabledEntity.Str1),
            nameof(NullableEnabledEntity.Str1).QuoteName(),
            typeof(string),
            typeof(string),
            true,
            new NullableEnabledEntity { Str1 = string.Empty },
            string.Empty,
        ];

        yield return
        [
            CreateAccessor<NullableEnabledEntity, string?>(entity => entity.Str1, referenceNullable: false),
            nameof(NullableEnabledEntity.Str1),
            nameof(NullableEnabledEntity.Str1).QuoteName(),
            typeof(string),
            typeof(string),
            true,
            new NullableEnabledEntity { Str1 = string.Empty },
            string.Empty,
        ];
    }

    public static IEnumerable<object[]> DataGeneratorForNotNullableContext()
    {
        // Int0
        yield return
        [
            CreateAccessor<NullableDisabledEntity, int>(entity => entity.Int0, referenceNullable: true),
            nameof(NullableDisabledEntity.Int0),
            nameof(NullableDisabledEntity.Int0).QuoteName(),
            typeof(int),
            typeof(int),
            false,
            new NullableDisabledEntity { Int0 = 1 },
            1,
        ];

        yield return
        [
            CreateAccessor<NullableDisabledEntity, int>(entity => entity.Int0, referenceNullable: false),
            nameof(NullableDisabledEntity.Int0),
            nameof(NullableDisabledEntity.Int0).QuoteName(),
            typeof(int),
            typeof(int),
            false,
            new NullableDisabledEntity { Int0 = 1 },
            1,
        ];

        // Int1
        yield return
        [
            CreateAccessor<NullableDisabledEntity, int?>(entity => entity.Int1, referenceNullable: true),
            nameof(NullableDisabledEntity.Int1),
            nameof(NullableDisabledEntity.Int1).QuoteName(),
            typeof(int?),
            typeof(int),
            true,
            new NullableDisabledEntity { Int1 = 1 },
            1,
        ];

        yield return
        [
            CreateAccessor<NullableDisabledEntity, int?>(entity => entity.Int1, referenceNullable: false),
            nameof(NullableDisabledEntity.Int1),
            nameof(NullableDisabledEntity.Int1).QuoteName(),
            typeof(int?),
            typeof(int),
            true,
            new NullableDisabledEntity { Int1 = 1 },
            1,
        ];

        //Str0
        yield return
        [
            CreateAccessor<NullableDisabledEntity, string>(entity => entity.Str0, referenceNullable: true),
            nameof(NullableDisabledEntity.Str0),
            nameof(NullableDisabledEntity.Str0).QuoteName(),
            typeof(string),
            typeof(string),
            true,
            new NullableDisabledEntity { Str0 = string.Empty },
            string.Empty,
        ];

        yield return
        [
            CreateAccessor<NullableDisabledEntity, string>(entity => entity.Str0, referenceNullable: false),
            nameof(NullableDisabledEntity.Str0),
            nameof(NullableDisabledEntity.Str0).QuoteName(),
            typeof(string),
            typeof(string),
            false,
            new NullableDisabledEntity { Str0 = string.Empty },
            string.Empty,
        ];

        //Str1
        yield return
        [
            CreateAccessor<NullableDisabledEntity, string?>(entity => entity.Str1, referenceNullable: true),
            nameof(NullableDisabledEntity.Str1),
            nameof(NullableDisabledEntity.Str1).QuoteName(),
            typeof(string),
            typeof(string),
            true,
            new NullableDisabledEntity { Str1 = null },
            null!,
        ];

        yield return
        [
            CreateAccessor<NullableDisabledEntity, string?>(entity => entity.Str1, referenceNullable: false),
            nameof(NullableDisabledEntity.Str1),
            nameof(NullableDisabledEntity.Str1).QuoteName(),
            typeof(string),
            typeof(string),
            false,
            new NullableDisabledEntity { Str1 = null },
            null!,
        ];
    }

    private static IPropertyAccessor<TEntry> CreateAccessor<TEntry, TProperty>(
        Expression<Func<TEntry, TProperty>> propertyAccessor,
        string? columnName = null,
        bool referenceNullable = true)
        where TEntry : class
    {
        return new PropertyAccessor<TEntry, TProperty>(propertyAccessor, columnName, referenceNullable);
    }
}
