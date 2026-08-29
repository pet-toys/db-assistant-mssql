using System;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// A fifteen-column row spanning the type families the library's own whitelist admits: the integer
/// widths, both string encodings, the exact and approximate numerics, the flag, the identifier, the
/// binary blob, a date and time, and a MAX column.
/// </summary>
/// <remarks>
/// The list is bounded by <c>TypeExtensions.IsSupportedType</c>. <see cref="DateTimeOffset"/> and
/// <see cref="TimeSpan"/> are absent from it, so <c>datetimeoffset</c> and <c>time</c> columns
/// cannot appear here even though SQL Server and <c>SqlBulkCopy</c> handle both. Whether that
/// whitelist should grow is a question about the library, not about this benchmark.
/// <para>
/// <see cref="Document"/> is deliberately small enough to stay in-row. A MAX value under 8000 bytes
/// is stored with the rest of the row and never takes the large-object path, so this shape spans the
/// MAX types without letting one column dominate the row. It is also what makes the shape the right
/// one for <c>InRowStreamingBenchmarks</c>: a copy with nothing to stream is what shows the price of
/// streaming anyway. The path itself belongs to <c>OffRowStreamingBenchmarks</c>.
/// </para>
/// </remarks>
public sealed class WideRow
{
    public required int Id { get; init; }

    public required long BigId { get; init; }

    public required short Small { get; init; }

    public required byte Tiny { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required char Initial { get; init; }

    public required decimal Amount { get; init; }

    public required double Ratio { get; init; }

    public required float Factor { get; init; }

    public required bool Flag { get; init; }

    public required Guid Identifier { get; init; }

    public required byte[] Payload { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required string Document { get; init; }
}
