using System;
using System.Linq.Expressions;
using System.Reflection;
using PetToys.DbAssistant.Mssql.Extensions;

namespace PetToys.DbAssistant.Mssql.Accessors;

internal sealed class PropertyAccessor<TEntity, TProperty> : IPropertyAccessor<TEntity>
    where TEntity : class
{
    private readonly Func<TEntity, TProperty> _getter;

    public PropertyAccessor(Expression<Func<TEntity, TProperty>> getter, string? columnName = null, bool referenceNullable = true)
    {
        var memberInfo = (getter.Body as MemberExpression)?.Member;
        if (memberInfo is null || memberInfo.MemberType != MemberTypes.Property)
        {
            throw new ArgumentException("Parameter " + nameof(getter) + " is not a property expression");
        }

        ClrType = typeof(TProperty);
        Type? nullableValueTypeUnderlyingType = null;
        if (ClrType.IsValueType) nullableValueTypeUnderlyingType = Nullable.GetUnderlyingType(ClrType);
        EffectiveType = nullableValueTypeUnderlyingType ?? ClrType;
        if (!EffectiveType.IsSupportedType()) throw new InvalidOperationException("Type " + EffectiveType.Name + " is not supported");
        PropertyName = memberInfo.Name;
        ColumnName = columnName?.QuoteName() ?? PropertyName.QuoteName();
        IsNullable = ClrType.IsValueType
            ? nullableValueTypeUnderlyingType is not null
            : new NullabilityInfoContext().Create((PropertyInfo)memberInfo).WriteState switch
            {
                NullabilityState.Nullable => true,
                NullabilityState.NotNull => false,
                _ => referenceNullable,
            };

        _getter = getter.Compile();
    }

    public Type ClrType { get; }

    public Type EffectiveType { get; }

    public string ColumnName { get; }

    public string PropertyName { get; }

    public bool IsNullable { get; }

    public object? GetValue(TEntity entity) => _getter(entity);
}
