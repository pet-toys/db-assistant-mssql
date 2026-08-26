using System;

namespace PetToys.DbAssistant.Mssql.Accessors;

/// <summary>
/// Reads a single mapped property from an entity and describes the destination
/// column its value is copied into.
/// </summary>
/// <typeparam name="TEntity">The entity type the property is read from.</typeparam>
public interface IPropertyAccessor<in TEntity>
    where TEntity : class
{
    /// <summary>
    /// Gets the name of the destination column, already quoted for SQL Server.
    /// </summary>
    string ColumnName { get; }

    /// <summary>
    /// Gets the name of the CLR property this accessor reads.
    /// </summary>
    string PropertyName { get; }

    /// <summary>
    /// Gets the declared type of the property, <see cref="Nullable{T}"/> included.
    /// </summary>
    Type ClrType { get; }

    /// <summary>
    /// Gets the type the column is described by: <see cref="ClrType"/> with
    /// <see cref="Nullable{T}"/> unwrapped to its underlying type.
    /// </summary>
    Type EffectiveType { get; }

    /// <summary>
    /// Gets a value indicating whether the column accepts nulls, taken from the
    /// nullability of the property.
    /// </summary>
    bool IsNullable { get; }

    /// <summary>
    /// Reads the property value from <paramref name="entity"/>.
    /// </summary>
    /// <param name="entity">The entity to read the property from.</param>
    /// <returns>
    /// The property value, or <see langword="null"/>, which is written to the
    /// column as <see cref="DBNull"/>.
    /// </returns>
    object? GetValue(TEntity entity);
}
