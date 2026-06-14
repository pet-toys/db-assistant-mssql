using System;
using PetToys.DbAssistant.Mssql.Accessors;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

/// <summary>
/// A single, strongly-typed expectation row for <see cref="PropertyAccessorTest"/>.
/// Replaces the previous untyped <c>object[]</c> theory rows so the
/// <c>xUnit1042</c> suppression is no longer required.
/// </summary>
/// <typeparam name="TEntity">The entity type the accessor targets.</typeparam>
public sealed record AccessorCase<TEntity>(
    string Description,
    IPropertyAccessor<TEntity> Accessor,
    string PropertyName,
    string ColumnName,
    Type ClrType,
    Type EffectiveType,
    bool IsNullable,
    TEntity Entity,
    object? Value)
    where TEntity : class
{
    // Surfaced as the test display name, so each case is identifiable in the runner.
    public override string ToString() => Description;
}
