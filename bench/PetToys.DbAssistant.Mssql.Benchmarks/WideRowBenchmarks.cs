using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The fifteen-column copy, spanning the type families the library's whitelist admits.
/// </summary>
/// <remarks>
/// The server's share of a copy grows faster with the row width than the mapping layer's does, so
/// this shape should dilute the library's overhead rather than multiply it. If it does not, the
/// overhead is per column in a way the narrow row cannot show.
/// </remarks>
public class WideRowBenchmarks : CopyBenchmark<WideRow>
{
    private const string Destination = "wide_row";

    [Params(10_000, 100_000)]
    public override int RowCount { get => base.RowCount; set => base.RowCount = value; }

    protected override string TableName => Destination;

    protected override IReadOnlyList<ColumnSpec> Columns => WideRowMapping.Columns;

    protected override IReadOnlyList<WideRow> GenerateRows(int count) => RowSet.Wide(count);

    protected override void Fill(object[] values, WideRow row)
    {
        values[0] = row.Id;
        values[1] = row.BigId;
        values[2] = row.Small;
        values[3] = row.Tiny;
        values[4] = row.Code;
        values[5] = row.Name;
        values[6] = row.Initial;
        values[7] = row.Amount;
        values[8] = row.Ratio;
        values[9] = row.Factor;
        values[10] = row.Flag;
        values[11] = row.Identifier;
        values[12] = row.Payload;
        values[13] = row.CreatedAt;
        values[14] = row.Document;
    }

    protected override DbDataReader CreateHandWrittenReader(IEnumerable<WideRow> rows) =>
        new WideRowReader(rows, Columns);

    protected override async ValueTask<long> CopyMappedAsync() =>
        await WideRowMapping.Map(Connection, Destination)
            .WriteDataAsync(Rows, ConfigureLikeTheOtherArms);
}
