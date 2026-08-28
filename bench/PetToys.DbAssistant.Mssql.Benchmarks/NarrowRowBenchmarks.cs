using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using PetToys.DbAssistant.Mssql.Extensions;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The four-column copy: an integer key, a name, a timestamp and a flag.
/// </summary>
/// <remarks>
/// The narrow row is where per-row and per-column overhead is least diluted by the server's own
/// share of a copy, so it is the harder of the two shapes for the library and the more informative
/// of the two about the mapping layer.
/// </remarks>
public class NarrowRowBenchmarks : CopyBenchmark<NarrowRow>
{
    private const string Destination = "narrow_row";

    [Params(10_000, 100_000)]
    public override int RowCount { get => base.RowCount; set => base.RowCount = value; }

    protected override string TableName => Destination;

    protected override IReadOnlyList<ColumnSpec> Columns { get; } =
    [
        new("Id", "int", typeof(int)),
        new("Name", "nvarchar(64)", typeof(string)),
        new("CreatedAt", "datetime2", typeof(DateTime)),
        new("Active", "bit", typeof(bool)),
    ];

    protected override IReadOnlyList<NarrowRow> GenerateRows(int count) => RowSet.Narrow(count);

    protected override void Fill(object[] values, NarrowRow row)
    {
        values[0] = row.Id;
        values[1] = row.Name;
        values[2] = row.CreatedAt;
        values[3] = row.Active;
    }

    protected override DbDataReader CreateHandWrittenReader(IEnumerable<NarrowRow> rows) =>
        new NarrowRowReader(rows, Columns);

    protected override async ValueTask<long> CopyMappedAsync() =>
        await Connection.CreateBulkContext<NarrowRow>(Destination)
            .MapProperty(row => row.Id)
            .MapProperty(row => row.Name)
            .MapProperty(row => row.CreatedAt)
            .MapProperty(row => row.Active)
            .WriteDataAsync(Rows, ConfigureLikeTheOtherArms);
}
