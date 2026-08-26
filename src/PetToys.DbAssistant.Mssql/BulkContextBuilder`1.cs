using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Accessors;

namespace PetToys.DbAssistant.Mssql;

/// <summary>
/// Provides a simple API for configuring an <see cref="SqlBulkCopy" />.
/// </summary>
/// <typeparam name="TEntity">The entity type being configured.</typeparam>
public sealed class BulkContextBuilder<TEntity>
    where TEntity : class
{
    private readonly SqlConnection _connection;
    private readonly string _tableName;
    private readonly List<IPropertyAccessor<TEntity>> _accessors = [];

    internal BulkContextBuilder(SqlConnection connection, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        _connection = connection;
        _tableName = tableName;
    }

    /// <summary>
    /// Maps a property to a column in the database.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property to be configured.</typeparam>
    /// <param name="propertyAccessor">A lambda expression representing the property to be mapped.</param>
    /// <param name="columnName">The name of the column. Default: <c>property name</c>.</param>
    /// <param name="referenceNullable">Used only if <see cref="NullabilityInfoContext"/> is not defined. (A directive <c>#nullable disable</c> is defined in the code).</param>
    /// <returns>An object that can be used to configure the <see cref="SqlBulkCopy"/></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public BulkContextBuilder<TEntity> MapProperty<TProperty>(Expression<Func<TEntity, TProperty>> propertyAccessor, string? columnName = null, bool referenceNullable = true)
    {
        var accessor = new PropertyAccessor<TEntity, TProperty>(propertyAccessor, columnName, referenceNullable);
        if (_accessors.Any(a => a.PropertyName == accessor.PropertyName)) throw new InvalidOperationException($"Property '{accessor.PropertyName}' is already mapped.");
        if (_accessors.Any(a => a.ColumnName == accessor.ColumnName)) throw new InvalidOperationException($"Column '{accessor.ColumnName}' is already mapped.");
        _accessors.Add(accessor);
        return this;
    }

    /// <summary>
    /// Writes data to the database using SqlBulkCopy.
    /// </summary>
    /// <remarks>
    /// The destination table name supplied to <c>CreateBulkContext</c> is passed to
    /// <see cref="SqlBulkCopy.DestinationTableName"/> verbatim: the caller owns quoting and
    /// multi-part qualification (e.g. <c>[schema].[table]</c>). This is the intentional asymmetry
    /// with column names, which the library always quotes.
    /// </remarks>
    /// <param name="entities">A collection of objects of type <typeparamref name="TEntity" />, intended to be saved to a database.</param>
    /// <param name="optionsBuilder">A delegate that is used to configure an <see cref="SqlBulkOptions"/>.</param>
    /// <param name="transaction">The transaction to use for this operation.</param>
    /// <param name="cancellationToken">The cancellation instruction.</param>
    /// <returns>Number of rows stored.</returns>
    /// <exception cref="ArgumentNullException">The <paramref name="entities"/> collection is null.</exception>
    /// <exception cref="InvalidOperationException">No property has been mapped via <see cref="MapProperty{TProperty}"/>.</exception>
    public async ValueTask<long> WriteDataAsync(
        IEnumerable<TEntity> entities,
        Action<SqlBulkOptions>? optionsBuilder = null,
        SqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (_accessors.Count == 0) throw new InvalidOperationException("At least one property must be mapped before writing.");
        var openedByThisCall = _connection.State != ConnectionState.Open;
        var options = new SqlBulkOptions();
        optionsBuilder?.Invoke(options);

        try
        {
            if (openedByThisCall) await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var copier = new SqlBulkCopy(_connection, options.CopyOptions, transaction);
            copier.DestinationTableName = _tableName;
            copier.EnableStreaming = options.EnableStreaming;
            copier.BulkCopyTimeout = options.BulkCopyTimeout;
            foreach (var accessor in _accessors)
            {
                copier.ColumnMappings.Add(accessor.PropertyName, accessor.ColumnName);
            }

            var reader = new EntityAccessor<TEntity>(entities, _accessors);
            await using (reader.ConfigureAwait(false))
            {
                await copier.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
                return copier.RowsCopied64;
            }
        }
        finally
        {
            if (openedByThisCall) await _connection.CloseAsync().ConfigureAwait(false);
        }
    }
}
