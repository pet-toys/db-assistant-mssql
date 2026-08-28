using System;
using System.Collections.Generic;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The floor for the wide row. Fifteen columns of direct property access, which is also fifteen
/// lines a caller has to keep in step with their schema by hand - the tedium this library exists to
/// remove, priced here so the removal can be judged against it.
/// </summary>
internal sealed class WideRowReader(IEnumerable<WideRow> rows, IReadOnlyList<ColumnSpec> columns)
    : RowReader<WideRow>(rows, columns)
{
    public override object GetValue(int ordinal) => ordinal switch
    {
        0 => Current.Id,
        1 => Current.BigId,
        2 => Current.Small,
        3 => Current.Tiny,
        4 => Current.Code,
        5 => Current.Name,
        6 => Current.Initial,
        7 => Current.Amount,
        8 => Current.Ratio,
        9 => Current.Factor,
        10 => Current.Flag,
        11 => Current.Identifier,
        12 => Current.Payload,
        13 => Current.CreatedAt,
        14 => Current.Document,
        _ => throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, UnknownOrdinal(ordinal)),
    };
}
