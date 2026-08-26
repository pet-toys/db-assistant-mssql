using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql;

/// <summary>
/// Configuration parameters.
/// </summary>
public sealed class SqlBulkOptions
{
    /// <summary>
    /// <see cref="SqlBulkCopyOptions"/> Default is <see cref="SqlBulkCopyOptions.Default"/>
    /// </summary>
    public SqlBulkCopyOptions CopyOptions { get; set; } = SqlBulkCopyOptions.Default;

    /// <summary>
    /// <see cref="SqlBulkCopy.EnableStreaming"/> Default is <c>true</c>.
    /// </summary>
    public bool EnableStreaming { get; set; } = true;

    /// <summary>
    /// <see cref="SqlBulkCopy.BulkCopyTimeout"/> Default is <c>0</c>.
    /// </summary>
    public int BulkCopyTimeout { get; set; }
}
