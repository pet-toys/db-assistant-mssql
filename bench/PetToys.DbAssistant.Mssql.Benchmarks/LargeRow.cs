namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// A two-column row whose MAX value is large enough that SQL Server stores it off-row.
/// </summary>
/// <remarks>
/// This shape exists only for <see cref="OffRowStreamingBenchmarks"/>. A MAX value under 8000 bytes lives
/// in-row and never takes the large-object path, which is the path
/// <c>SqlBulkOptions.EnableStreaming</c> changes; a value over it does. Nothing else is in the row,
/// because everything else would be noise around the one column the class is about.
/// </remarks>
public sealed class LargeRow
{
    public required int Id { get; init; }

    public required string Document { get; init; }
}
