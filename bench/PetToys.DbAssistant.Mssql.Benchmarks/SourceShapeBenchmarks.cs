using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using PetToys.DbAssistant.Mssql.Extensions;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The two <c>WriteDataAsync</c> overloads against each other, over the narrow row.
/// </summary>
/// <remarks>
/// Both overloads share one copy path and differ only in how they walk the rows, so what is measured
/// here is the enumeration: a <c>foreach</c> against an <c>await foreach</c> over an iterator that
/// never actually suspends. A caller streaming from a real source pays whatever that source costs on
/// top; this pair only says what the overload itself adds, which is the part this repository can be
/// held to.
/// <para>
/// There is no <c>DataTable</c> arm and no reader arm here. Both would answer a question this class
/// is not asking, and the row-shape classes already ask it.
/// </para>
/// </remarks>
public class SourceShapeBenchmarks : BulkCopyHarness<NarrowRow>
{
    private const string Destination = "source_shape_row";

    [Params(10_000, 100_000)]
    public override int RowCount { get => base.RowCount; set => base.RowCount = value; }

    protected override string TableName => Destination;

    // The same four columns NarrowRowBenchmarks uses, in a table of their own: two classes sharing
    // one destination would have each other's leftovers to truncate, and BenchmarkDotNet runs them
    // in processes that know nothing about one another.
    protected override IReadOnlyList<ColumnSpec> Columns { get; } =
    [
        new("Id", "int", typeof(int)),
        new("Name", "nvarchar(64)", typeof(string)),
        new("CreatedAt", "datetime2", typeof(DateTime)),
        new("Active", "bit", typeof(bool)),
    ];

    protected override IReadOnlyList<NarrowRow> GenerateRows(int count) => RowSet.Narrow(count);

    [Benchmark(Baseline = true, Description = "IEnumerable source")]
    public async Task<long> SynchronousAsync() =>
        await Map().WriteDataAsync(Rows, ConfigureLikeTheOtherArms);

    [Benchmark(Description = "IAsyncEnumerable source")]
    public async Task<long> AsynchronousAsync() =>
        await Map().WriteDataAsync(AsAsyncEnumerable(Rows), ConfigureLikeTheOtherArms);

    private static async IAsyncEnumerable<NarrowRow> AsAsyncEnumerable(IReadOnlyList<NarrowRow> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }

        await Task.CompletedTask;
    }

    private BulkContextBuilder<NarrowRow> Map() =>
        Connection.CreateBulkContext<NarrowRow>(Destination)
            .MapProperty(row => row.Id)
            .MapProperty(row => row.Name)
            .MapProperty(row => row.CreatedAt)
            .MapProperty(row => row.Active);
}
