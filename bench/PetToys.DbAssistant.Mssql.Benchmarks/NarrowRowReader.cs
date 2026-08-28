using System;
using System.Collections.Generic;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The floor for the narrow row: a reader written for exactly one type, switching on the ordinal
/// into direct property access, with no reflection and no delegate anywhere.
/// </summary>
/// <remarks>
/// Nobody arrives at this library from here - a caller who has already written one of these per
/// entity type is not shopping for a wrapper. It is measured because it bounds the comparison: the
/// distance between this arm and the mapped one is the most the library's own machinery can be
/// charged with, and the distance between this arm and the <c>DataTable</c> baseline is what the
/// approach itself is worth.
/// </remarks>
internal sealed class NarrowRowReader(IEnumerable<NarrowRow> rows, IReadOnlyList<ColumnSpec> columns)
    : RowReader<NarrowRow>(rows, columns)
{
    public override object GetValue(int ordinal) => ordinal switch
    {
        0 => Current.Id,
        1 => Current.Name,
        2 => Current.CreatedAt,
        3 => Current.Active,
        _ => throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, UnknownOrdinal(ordinal)),
    };
}
