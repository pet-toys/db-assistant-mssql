using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql;

/// <summary>
/// Configuration parameters.
/// </summary>
public sealed class SqlBulkOptions
{
    /// <summary>
    /// The options the copy runs with. Defaults to <see cref="SqlBulkCopyOptions.Default"/>,
    /// which is what an unconfigured <see cref="SqlBulkCopy"/> uses.
    /// </summary>
    public SqlBulkCopyOptions CopyOptions { get; set; } = SqlBulkCopyOptions.Default;

    /// <summary>
    /// Whether each value is written to the server as it is read rather than materialised first.
    /// Defaults to <c>false</c>, which is <see cref="SqlBulkCopy.EnableStreaming"/>'s own default.
    /// </summary>
    /// <remarks>
    /// Turn it on for rows carrying a MAX column whose values are large enough that SQL Server
    /// stores them off-row. Streaming writes such a value without holding it in memory, and that
    /// is the case the flag exists for. Where every value fits in-row it buys nothing and costs an
    /// allocation per column per row, so it stays off unless a caller asks for it.
    /// </remarks>
    public bool EnableStreaming { get; set; }

    /// <summary>
    /// How long the copy may run before it is abandoned, in seconds, <c>0</c> meaning no limit.
    /// Defaults to <c>0</c> rather than to <see cref="SqlBulkCopy.BulkCopyTimeout"/>'s thirty
    /// seconds.
    /// </summary>
    /// <remarks>
    /// The departure from the provider is deliberate. A copy this library is written for can carry
    /// millions of rows and run for minutes, and thirty seconds would abandon it part-written by
    /// default. A caller who wants a bound is the one who knows what it should be.
    /// </remarks>
    public int BulkCopyTimeout { get; set; }
}
