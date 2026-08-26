using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql.Extensions;

/// <summary>
/// Extension methods on <see cref="SqlConnection"/> that start a bulk-copy
/// configuration.
/// </summary>
public static class SqlConnectionExtensions
{
    /// <summary>
    /// Creates a <see cref="BulkContextBuilder{TEntity}"/> for the specified entity type.
    /// </summary>
    /// <remarks>
    /// <paramref name="tableName"/> is passed to <c>SqlBulkCopy.DestinationTableName</c> verbatim:
    /// the library validates only that it is present (non-null, non-whitespace) and does not quote
    /// or normalize it. The caller owns quoting and multi-part qualification (e.g.
    /// <c>[schema].[table]</c> or <c>db.schema.table</c>). This is the intentional asymmetry with
    /// column names, which the library always quotes.
    /// </remarks>
    /// <typeparam name="TEntity">The entity type being configured.</typeparam>
    /// <param name="connection"><see cref="SqlConnection"/></param>
    /// <param name="tableName">The name of the target database table, passed through to SQL Server verbatim.</param>
    /// <returns><see cref="BulkContextBuilder{TEntity}"/></returns>
    public static BulkContextBuilder<TEntity> CreateBulkContext<TEntity>(this SqlConnection connection, string tableName)
        where TEntity : class =>
            new(connection, tableName);
}
