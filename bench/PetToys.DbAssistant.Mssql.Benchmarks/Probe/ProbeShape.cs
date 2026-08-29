using System.Collections.Generic;
using System.Globalization;

namespace PetToys.DbAssistant.Mssql.Benchmarks.Probe;

/// <summary>
/// A row shape the probe can measure: what its columns are, where they land, and how to build a
/// source of them.
/// </summary>
/// <remarks>
/// The columns, the mapping and the generator all come from the same declarations the benchmark
/// classes use. A probe result is read beside <c>BASELINE.md</c>, and it can only be read that way
/// while both are copying the same columns in the same order with the same options into the same
/// column types.
/// </remarks>
internal abstract class ProbeShape
{
    /// <summary>The four-column shape.</summary>
    public static ProbeShape Narrow { get; } = new NarrowShape();

    /// <summary>The fifteen-column shape.</summary>
    public static ProbeShape Wide { get; } = new WideShape();

    /// <summary>The shape's name as it is written on the command line and in the result.</summary>
    public abstract string Name { get; }

    /// <summary>The destination columns, in the order both mechanisms write them.</summary>
    public abstract IReadOnlyList<ColumnSpec> Columns { get; }

    /// <summary>
    /// The stem of the destination tables. Deliberately not the benchmark's table: a probe attempt
    /// dies part-way through a copy by design, and it should not be leaving a benchmark's
    /// destination in that state.
    /// </summary>
    protected abstract string TableStem { get; }

    /// <summary>Resolves a shape by the name used on the command line.</summary>
    /// <param name="name">The shape name.</param>
    /// <returns>The shape, or <c>null</c> if the name matches none.</returns>
    public static ProbeShape? Parse(string name) => name switch
    {
        "narrow" => Narrow,
        "wide" => Wide,
        _ => null,
    };

    /// <summary>
    /// The unquoted destination table of one copy within an attempt.
    /// </summary>
    /// <remarks>
    /// Every simultaneous copy gets a table of its own so that none of them waits on a lock another
    /// holds. Copies into one table are what a real caller does and are legal against a heap under
    /// <c>TABLOCK</c>, but a lock wait would put the boundary where the server's locking falls
    /// rather than where the client's memory does, and a deadlock exits non-zero, which this probe
    /// reads as "did not fit" - a wrong answer rather than a missing one.
    /// </remarks>
    /// <param name="index">The copy's index within its attempt.</param>
    public string TableNameFor(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{TableStem}_{index:D2}");

    /// <summary>Builds one source of this shape.</summary>
    /// <param name="rowCount">How many rows the source holds.</param>
    /// <param name="shareText">
    /// Whether the source may draw its text from the generator's pools. Sharing makes the source
    /// artificially light, and the source is the cost both mechanisms pay, so it moves the ratio.
    /// The probe measures both settings rather than choosing one.
    /// </param>
    public abstract ProbeSource CreateSource(int rowCount, bool shareText);

    private sealed class NarrowShape : ProbeShape
    {
        public override string Name => "narrow";

        public override IReadOnlyList<ColumnSpec> Columns => NarrowRowMapping.Columns;

        protected override string TableStem => "probe_narrow_row";

        public override ProbeSource CreateSource(int rowCount, bool shareText) =>
            ProbeSource.Narrow(rowCount, shareText);
    }

    private sealed class WideShape : ProbeShape
    {
        public override string Name => "wide";

        public override IReadOnlyList<ColumnSpec> Columns => WideRowMapping.Columns;

        protected override string TableStem => "probe_wide_row";

        public override ProbeSource CreateSource(int rowCount, bool shareText) =>
            ProbeSource.Wide(rowCount, shareText);
    }
}
