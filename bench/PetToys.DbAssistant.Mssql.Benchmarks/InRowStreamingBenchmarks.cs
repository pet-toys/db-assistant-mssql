using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// <c>SqlBulkOptions.EnableStreaming</c> against itself over the wide row, whose every value is
/// stored in-row - the half of the question that decides what the library's default costs an
/// ordinary caller.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OffRowStreamingBenchmarks"/> measures this flag where it was designed to help: a
/// document too large to sit in-row is read as a stream instead of being materialised, and the
/// saving is real. But that shape is the exception. Most rows anyone copies are the shape of
/// <see cref="WideRow"/>, and on those the flag has nothing to stream and still pays for the
/// machinery, which is a cost the off-row class is structurally unable to report.
/// </para>
/// <para>
/// The class exists because that cost turned out to be the largest single effect in this project,
/// and it was found by hand, by flipping a constant between two runs, which is not a result the
/// repository can keep. Reproducing it as two arms of one class puts it in the recorded baseline
/// where a later change can contradict it.
/// </para>
/// <para>
/// The shape and the row counts are the wide row's on purpose, so these numbers can be read
/// directly beside <see cref="WideRowBenchmarks"/>: the mapped arm there is this class's baseline
/// arm, copying the same rows into an identically-shaped table.
/// </para>
/// </remarks>
public class InRowStreamingBenchmarks : BulkCopyHarness<WideRow>
{
    private const string Destination = "in_row_streaming_row";

    [Params(10_000, 100_000)]
    public override int RowCount { get => base.RowCount; set => base.RowCount = value; }

    protected override string TableName => Destination;

    protected override IReadOnlyList<ColumnSpec> Columns => WideRowMapping.Columns;

    protected override IReadOnlyList<WideRow> GenerateRows(int count) => RowSet.Wide(count);

    [Benchmark(Baseline = true, Description = "EnableStreaming on (the library default)")]
    public async Task<long> StreamingOnAsync() =>
        await WideRowMapping.Map(Connection, Destination)
            .WriteDataAsync(Rows, ConfigureLikeTheOtherArms);

    [Benchmark(Description = "EnableStreaming off")]
    public async Task<long> StreamingOffAsync() =>
        await WideRowMapping.Map(Connection, Destination)
            .WriteDataAsync(Rows, ConfigureWithStreamingOff);
}
