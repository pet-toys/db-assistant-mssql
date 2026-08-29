using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql.Benchmarks.Probe;

/// <summary>
/// A row shape the probe can measure, and the two mechanisms it measures over it.
/// </summary>
/// <remarks>
/// <para>
/// The columns, the mapping and the generator all come from the same declarations the benchmark
/// classes use. A probe result is read beside <c>BASELINE.md</c>, and it can only be read that way
/// while both are copying the same columns in the same order with the same options into the same
/// column types.
/// </para>
/// <para>
/// The source collection is built inside <see cref="CopyAsync"/> rather than handed in, because it
/// is the caller's unavoidable cost, it is identical for both mechanisms, and it counts against the
/// same ceiling. Building it outside the measured process would measure the mechanism in isolation,
/// which is not a situation anybody is in.
/// </para>
/// </remarks>
internal abstract class ProbeShape
{
    /// <summary>The four-column shape.</summary>
    public static ProbeShape Narrow { get; } = new NarrowShape();

    /// <summary>The fifteen-column shape.</summary>
    public static ProbeShape Wide { get; } = new WideShape();

    /// <summary>The shape's name as it is written on the command line and in the result.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// The unquoted destination table. Deliberately not the benchmark's table: a probe attempt dies
    /// part-way through a copy by design, and it should not be leaving a benchmark's destination in
    /// that state.
    /// </summary>
    public abstract string TableName { get; }

    /// <summary>The destination columns, in the order both mechanisms write them.</summary>
    public abstract IReadOnlyList<ColumnSpec> Columns { get; }

    /// <summary>Resolves a shape by the name used on the command line.</summary>
    /// <param name="name">The shape name.</param>
    /// <returns>The shape, or <c>null</c> if the name matches none.</returns>
    public static ProbeShape? Parse(string name) => name switch
    {
        "narrow" => Narrow,
        "wide" => Wide,
        _ => null,
    };

    /// <summary>Builds the source at the given row count and copies it by the given mechanism.</summary>
    /// <param name="connection">The open connection to copy through.</param>
    /// <param name="mechanism">Which of the two routes to take.</param>
    /// <param name="rowCount">How many rows to build and copy.</param>
    /// <param name="shareText">
    /// Whether the source may draw its text from the generator's pools. Sharing makes the source
    /// artificially light, and the source is the cost both mechanisms pay, so it moves the ratio.
    /// The probe measures both settings rather than choosing one.
    /// </param>
    /// <returns>The number of rows the server reports having received.</returns>
    public abstract Task<long> CopyAsync(
        SqlConnection connection,
        ProbeMechanism mechanism,
        int rowCount,
        bool shareText);

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

    private sealed class NarrowShape : ProbeShape
    {
        public override string Name => "narrow";

        public override string TableName => "probe_narrow_row";

        public override IReadOnlyList<ColumnSpec> Columns => NarrowRowMapping.Columns;

        public override async Task<long> CopyAsync(
            SqlConnection connection,
            ProbeMechanism mechanism,
            int rowCount,
            bool shareText)
        {
            var rows = RowSet.Narrow(rowCount, shareText);

            switch (mechanism)
            {
                case ProbeMechanism.DataTable:
                    return await CopyThroughDataTableAsync(
                        connection, TableName, Columns, rows, NarrowRowMapping.Fill);
                case ProbeMechanism.MappedBulkContext:
                    return await NarrowRowMapping.Map(connection, TableName)
                        .WriteDataAsync(rows, CopySettings.Apply);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mechanism));
            }
        }
    }

    private sealed class WideShape : ProbeShape
    {
        public override string Name => "wide";

        public override string TableName => "probe_wide_row";

        public override IReadOnlyList<ColumnSpec> Columns => WideRowMapping.Columns;

        public override async Task<long> CopyAsync(
            SqlConnection connection,
            ProbeMechanism mechanism,
            int rowCount,
            bool shareText)
        {
            var rows = RowSet.Wide(rowCount, shareText);

            switch (mechanism)
            {
                case ProbeMechanism.DataTable:
                    return await CopyThroughDataTableAsync(
                        connection, TableName, Columns, rows, WideRowMapping.Fill);
                case ProbeMechanism.MappedBulkContext:
                    return await WideRowMapping.Map(connection, TableName)
                        .WriteDataAsync(rows, CopySettings.Apply);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mechanism));
            }
        }
    }
}
