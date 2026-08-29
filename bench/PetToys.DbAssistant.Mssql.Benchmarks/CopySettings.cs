using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The one declaration of how every copy in this project is configured, whether it is a benchmark
/// arm or a probe attempt.
/// </summary>
/// <remarks>
/// Two copies are only comparable if the <see cref="SqlBulkCopy"/> underneath them is configured
/// identically, and a difference in any one setting invalidates the comparison without appearing
/// anywhere in its output. That is true across the project as well as within one benchmark class:
/// a probe result is read beside the recorded baseline, so it has to have been taken with the same
/// options the baseline was.
/// </remarks>
internal static class CopySettings
{
    /// <summary>
    /// Required for minimal logging into a heap, and applied everywhere so that it cancels out of
    /// every ratio rather than favouring one side of one.
    /// </summary>
    public const SqlBulkCopyOptions Options = SqlBulkCopyOptions.TableLock;

    /// <summary>
    /// Mirrors <c>SqlBulkOptions.EnableStreaming</c>, which agrees with
    /// <see cref="SqlBulkCopy.EnableStreaming"/> that the default is <c>false</c>. A mirror and not
    /// a choice: if the library's default moves, this moves with it, or the measurements stop
    /// describing what an unconfigured caller gets.
    /// </summary>
    public const bool EnableStreaming = false;

    /// <summary>
    /// No limit. The library's own default, and the only workable one here: a probe attempt
    /// deliberately runs until it either finishes or dies, and a timeout would end it a third way.
    /// </summary>
    public const int Timeout = 0;

    /// <summary>Builds a copier configured the way every raw arm and every probe attempt copies.</summary>
    /// <param name="connection">The open connection to copy through.</param>
    /// <param name="tableName">The unquoted destination table name.</param>
    /// <param name="columns">The destination columns, in the order they are written.</param>
    public static SqlBulkCopy CreateCopier(
        SqlConnection connection,
        string tableName,
        IReadOnlyList<ColumnSpec> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var copier = new SqlBulkCopy(connection, Options, externalTransaction: null)
        {
            DestinationTableName = tableName,
            EnableStreaming = EnableStreaming,
            BulkCopyTimeout = Timeout,
        };

        foreach (var column in columns)
        {
            // The destination is bracket-quoted because the library quotes it, and the source is not
            // because the library passes the property name through unquoted. Identical mappings, not
            // merely equivalent ones.
            copier.ColumnMappings.Add(column.Name, $"[{column.Name}]");
        }

        return copier;
    }

    /// <summary>Puts a mapped copy on exactly the configuration <see cref="CreateCopier"/> builds.</summary>
    /// <param name="options">The options the library is about to copy with.</param>
    public static void Apply(SqlBulkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.CopyOptions = Options;
        options.EnableStreaming = EnableStreaming;
        options.BulkCopyTimeout = Timeout;
    }

    /// <summary>
    /// The <c>CREATE TABLE</c> statement for a destination generated from its column declaration,
    /// so that the schema cannot drift from what the arms write into it.
    /// </summary>
    /// <param name="tableName">The unquoted destination table name.</param>
    /// <param name="columns">The destination columns, in the order they are written.</param>
    public static string BuildCreateTableStatement(string tableName, IReadOnlyList<ColumnSpec> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        return $"CREATE TABLE [{tableName}] (" +
            string.Join(", ", columns.Select(column => $"[{column.Name}] {column.DataType} NOT NULL")) +
            ");";
    }
}
