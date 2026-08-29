using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using PetToys.DbAssistant.Mssql.Extensions;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// <c>SqlBulkOptions.EnableStreaming</c> against itself, over rows whose MAX column is stored
/// off-row - the half of the question where turning the flag on pays.
/// </summary>
/// <remarks>
/// <para>
/// This class exists because the library used to override ADO.NET here, defaulting
/// <c>SqlBulkOptions.EnableStreaming</c> to <c>true</c> where <c>SqlBulkCopy.EnableStreaming</c>
/// defaults to <c>false</c>, with nothing measuring what that chose on the caller's behalf. The
/// two agree now, and the arms answer the question a caller is left with instead: the default is
/// the baseline, so the ratio reads as what turning streaming back on buys, and this is the row
/// shape where the answer is that it is worth doing.
/// </para>
/// <para>
/// This class answers only half of that. Streaming is a decision about how a value reaches the
/// wire, so its cost depends on how large the values are, and a class built on off-row documents
/// asks about the favourable half only. <see cref="InRowStreamingBenchmarks"/> asks the other half
/// over ordinary rows. Neither is the answer on its own, and reading this one alone is how a
/// default gets confirmed by the only measurement that flatters it.
/// </para>
/// <para>
/// The row shape is not the wide row's. A <c>varchar(max)</c> value under
/// <see cref="RowSet.InRowThreshold"/> bytes is stored in-row and never takes the large-object path,
/// which is the path this flag changes, so a class built on the wide row would compare a setting
/// against itself and report a ratio of one. The row counts are lower than the row-shape classes'
/// for the same reason the values are larger: a hundred thousand off-row documents is most of a
/// gigabyte.
/// </para>
/// </remarks>
public class OffRowStreamingBenchmarks : BulkCopyHarness<LargeRow>
{
    private const string Destination = "streaming_row";

    [Params(1_000, 5_000)]
    public override int RowCount { get => base.RowCount; set => base.RowCount = value; }

    protected override string TableName => Destination;

    protected override IReadOnlyList<ColumnSpec> Columns { get; } =
    [
        new("Id", "int", typeof(int)),
        new("Document", "varchar(max)", typeof(string)),
    ];

    protected override IReadOnlyList<LargeRow> GenerateRows(int count) => RowSet.Large(count);

    [Benchmark(Baseline = true, Description = "EnableStreaming off (the default)")]
    public async Task<long> StreamingOffAsync() =>
        await Map().WriteDataAsync(Rows, ConfigureLikeTheOtherArms);

    [Benchmark(Description = "EnableStreaming on")]
    public async Task<long> StreamingOnAsync() =>
        await Map().WriteDataAsync(Rows, ConfigureWithStreamingOn);

    /// <summary>
    /// Fails the setup rather than the reading of the report if the documents would sit in-row. A
    /// value under the threshold makes both arms take the same path, and the class would then report
    /// a ratio of one that looks like a finding and is an accident of the row shape.
    /// </summary>
    /// <remarks>
    /// This checks the CLR-side length, which is what the run can check cheaply and every time. That
    /// SQL Server did put the values in a LOB allocation unit is confirmed once, by hand, when the
    /// baseline is recorded: <c>sys.dm_db_index_physical_stats</c> reports an <c>LOB_DATA</c> unit
    /// for the destination once it has been written.
    /// </remarks>
    protected override void OnRowsBuilt()
    {
        var shortest = Rows.Min(row => row.Document.Length);

        if (shortest <= RowSet.InRowThreshold)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The shortest document is {shortest} bytes, at or under the {RowSet.InRowThreshold}-byte in-row threshold. This class would measure the in-row path and report a ratio that means nothing."));
        }
    }

    private BulkContextBuilder<LargeRow> Map() =>
        Connection.CreateBulkContext<LargeRow>(Destination)
            .MapProperty(row => row.Id)
            .MapProperty(row => row.Document);
}
