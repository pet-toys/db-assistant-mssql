using System;

namespace PetToys.DbAssistant.Mssql.Accessors;

public interface IPropertyAccessor<in TEntity>
    where TEntity : class
{
    string ColumnName { get; }

    string PropertyName { get; }

    Type ClrType { get; }

    Type EffectiveType { get; }

    bool IsNullable { get; }

    object? GetValue(TEntity entity);
}
