using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql.Benchmarks.Probe;

/// <summary>
/// One materialised collection of rows, and the two ways of copying it.
/// </summary>
/// <remarks>
/// <para>
/// Building the rows and copying them are separate steps because a concurrency attempt needs every
/// one of its sources to exist before any of its copies starts. A burst is requests whose bodies
/// have already arrived and already been deserialised, so they coexist whether or not the copies
/// overlap, and it is their sum the ceiling has to hold. Building each source as its own copy began
/// would let the peak depend on how the copies happened to interleave, which is a property of the
/// scheduler rather than of either mechanism.
/// </para>
/// <para>
/// A single-copy attempt builds one and copies it, which is the same two steps with nothing between
/// them.
/// </para>
/// </remarks>
internal abstract class ProbeSource
{
    /// <summary>Builds the narrow shape's rows.</summary>
    /// <param name="rowCount">How many rows to build.</param>
    /// <param name="shareText">Whether the rows may draw their text from the generator's pools.</param>
    public static ProbeSource Narrow(int rowCount, bool shareText) =>
        new NarrowSource(RowSet.Narrow(rowCount, shareText));

    /// <summary>Builds the wide shape's rows.</summary>
    /// <param name="rowCount">How many rows to build.</param>
    /// <param name="shareText">Whether the rows may draw their text from the generator's pools.</param>
    public static ProbeSource Wide(int rowCount, bool shareText) =>
        new WideSource(RowSet.Wide(rowCount, shareText));

    /// <summary>Copies these rows by the given mechanism.</summary>
    /// <param name="connection">The open connection to copy through.</param>
    /// <param name="tableName">The unquoted destination table, which no other copy writes to.</param>
    /// <param name="mechanism">Which of the two routes to take.</param>
    /// <returns>The number of rows the server reports having received.</returns>
    public abstract Task<long> CopyAsync(SqlConnection connection, string tableName, ProbeMechanism mechanism);

    /// <summary>
    /// The route a caller has today: every row materialised into a <c>DataTable</c>, which is then
    /// handed to <see cref="SqlBulkCopy"/>.
    /// </summary>
    /// <remarks>
    /// Presized to the row count, exactly as the benchmark's baseline arm is. An unsized table grows
    /// by doubling and would put the boundary where its own regrowth falls rather than where its
    /// contents do.
    /// </remarks>
    /// <typeparam name="TRow">The row type being copied.</typeparam>
    /// <param name="connection">The open connection to copy through.</param>
    /// <param name="tableName">The unquoted destination table.</param>
    /// <param name="columns">The destination columns, in write order.</param>
    /// <param name="rows">The source collection.</param>
    /// <param name="fill">Writes one row's values into a buffer, in column order.</param>
    protected static async Task<long> CopyThroughDataTableAsync<TRow>(
        SqlConnection connection,
        string tableName,
        IReadOnlyList<ColumnSpec> columns,
        IReadOnlyList<TRow> rows,
        Action<object[], TRow> fill)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(fill);

        using var table = new DataTable
        {
            Locale = CultureInfo.InvariantCulture,
            MinimumCapacity = rows.Count,
        };

        foreach (var column in columns)
        {
            table.Columns.Add(column.Name, column.ClrType);
        }

        var values = new object[columns.Count];

        foreach (var row in rows)
        {
            fill(values, row);
            table.Rows.Add(values);
        }

        using var copier = CopySettings.CreateCopier(connection, tableName, columns);
        await copier.WriteToServerAsync(table);
        return copier.RowsCopied64;
    }

    private sealed class NarrowSource(IReadOnlyList<NarrowRow> rows) : ProbeSource
    {
        public override async Task<long> CopyAsync(
            SqlConnection connection,
            string tableName,
            ProbeMechanism mechanism) => mechanism switch
            {
                ProbeMechanism.DataTable => await CopyThroughDataTableAsync(
                    connection, tableName, NarrowRowMapping.Columns, rows, NarrowRowMapping.Fill),
                ProbeMechanism.MappedBulkContext => await NarrowRowMapping.Map(connection, tableName)
                    .WriteDataAsync(rows, CopySettings.Apply),
                _ => throw new ArgumentOutOfRangeException(nameof(mechanism)),
            };
    }

    private sealed class WideSource(IReadOnlyList<WideRow> rows) : ProbeSource
    {
        public override async Task<long> CopyAsync(
            SqlConnection connection,
            string tableName,
            ProbeMechanism mechanism) => mechanism switch
            {
                ProbeMechanism.DataTable => await CopyThroughDataTableAsync(
                    connection, tableName, WideRowMapping.Columns, rows, WideRowMapping.Fill),
                ProbeMechanism.MappedBulkContext => await WideRowMapping.Map(connection, tableName)
                    .WriteDataAsync(rows, CopySettings.Apply),
                _ => throw new ArgumentOutOfRangeException(nameof(mechanism)),
            };
    }
}
