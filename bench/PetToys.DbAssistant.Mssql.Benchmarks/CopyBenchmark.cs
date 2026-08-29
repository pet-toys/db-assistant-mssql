using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The four arms of a row-shape comparison, chosen by one question: what does a caller have before
/// they install this package?
/// </summary>
/// <typeparam name="TRow">The row type being copied.</typeparam>
/// <remarks>
/// <para>
/// The arms live here rather than in each derived class so that no derived class can configure one
/// of them differently from the rest. A derived class supplies the columns, the rows, a hand-written
/// reader and the mapped copy, and nothing else.
/// </para>
/// <para>
/// The <c>DataTable</c> arm is the baseline because it is the canonical snippet, the one every
/// article and answer reaches for, and therefore the thing people migrate from. The two reader arms
/// are kept separate on purpose: the reflective one is the other migration source, and the
/// hand-written one is the floor. Collapsing them into a single "hand-written baseline" would hide
/// an order of magnitude in per-value cost between two things written by different people for
/// different reasons.
/// </para>
/// </remarks>
public abstract class CopyBenchmark<TRow> : BulkCopyHarness<TRow>
    where TRow : class
{
    /// <summary>Writes one row's values into a buffer, in column order, for the baseline's table.</summary>
    /// <param name="values">The buffer to fill; its length is the column count.</param>
    /// <param name="row">The row to read.</param>
    protected abstract void Fill(object[] values, TRow row);

    /// <summary>Creates the hand-written reader for this row type.</summary>
    /// <param name="rows">The rows the reader walks.</param>
    protected abstract DbDataReader CreateHandWrittenReader(IEnumerable<TRow> rows);

    /// <summary>Copies the rows through the library, mapping every column.</summary>
    protected abstract ValueTask<long> CopyMappedAsync();

    /// <summary>
    /// The approach a caller has today: every row materialised into a <c>DataTable</c>, which is
    /// then handed to <see cref="SqlBulkCopy"/>. The baseline, because it is what people migrate
    /// from.
    /// </summary>
    /// <remarks>
    /// The table is built inside the timed region, and that is the whole point of the arm. The
    /// package's claim is "no intermediate DataTable", so the cost of the intermediate DataTable -
    /// materialising every row as boxed values and holding them until the copy finishes - is
    /// precisely what is being priced. Building it once in setup and copying it fifteen times, as
    /// an earlier revision did, hands the baseline a pre-built, pre-boxed table for free and
    /// subtracts the one quantity the comparison exists to measure. A caller with rows in memory
    /// pays for this on every copy, and so does this arm.
    /// </remarks>
    [Benchmark(Baseline = true, Description = "DataTable")]
    public async Task<long> CopyDataTableAsync()
    {
        using var table = BuildDataTable(Rows);
        using var copier = CreateCopier();
        await copier.WriteToServerAsync(table);
        return copier.RowsCopied64;
    }

    /// <summary>
    /// The other approach a caller has today: a reader that resolves every value through
    /// <c>PropertyInfo.GetValue</c>, which is what somebody writes when they want one reader to work
    /// for every entity type.
    /// </summary>
    [Benchmark(Description = "Reflective reader")]
    public async Task<long> CopyReflectiveAsync()
    {
        await using var reader = new ReflectiveRowReader<TRow>(Rows, Columns);
        return await CopyReaderAsync(reader);
    }

    /// <summary>
    /// The floor: a reader written for exactly one row type, switching on the ordinal into direct
    /// property access, with no reflection anywhere. Nobody's starting point, but it bounds how much
    /// of the gap between the baseline and the mapped arm can be charged to this library at all.
    /// </summary>
    [Benchmark(Description = "Hand-written reader")]
    public async Task<long> CopyHandWrittenAsync()
    {
        await using var reader = CreateHandWrittenReader(Rows);
        return await CopyReaderAsync(reader);
    }

    /// <summary>The measured arm: the same copy through this library's fluent mapping.</summary>
    [Benchmark(Description = "Mapped bulk context")]
    public async Task<long> CopyMappedBenchmarkAsync() => await CopyMappedAsync();

    private DataTable BuildDataTable(IReadOnlyList<TRow> rows)
    {
        // Presized rather than left to grow. DataRowCollection doubles from a small initial
        // capacity, so an unsized table charges the baseline for its own regrowth on top of the
        // rows it holds, which is a cost of not knowing the count rather than a cost of the
        // approach. The caller this arm stands for has a materialised collection and therefore
        // knows the count too, so leaving it unsized would be measuring a handicap nobody has.
        var table = new DataTable
        {
            Locale = CultureInfo.InvariantCulture,
            MinimumCapacity = rows.Count,
        };

        foreach (var column in Columns)
        {
            table.Columns.Add(column.Name, column.ClrType);
        }

        var values = new object[Columns.Count];

        foreach (var row in rows)
        {
            Fill(values, row);
            table.Rows.Add(values);
        }

        return table;
    }
}
