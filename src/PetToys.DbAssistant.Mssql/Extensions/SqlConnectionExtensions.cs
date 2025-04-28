using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql.Extensions;

public static class SqlConnectionExtensions
{
    /// <summary>
    /// Creates a <see cref="BulkContextBuilder{TEntity}"/> for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being configured.</typeparam>
    /// <param name="connection"><see cref="SqlConnection"/></param>
    /// <param name="tableName">The name of the target database table.</param>
    /// <returns><see cref="BulkContextBuilder{TEntity}"/></returns>
    public static BulkContextBuilder<TEntity> CreateBulkContext<TEntity>(this SqlConnection connection, string tableName)
        where TEntity : class =>
            new(connection, tableName);
}
