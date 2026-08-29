using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The reader somebody writes when they want one reader to work for every entity type: columns are
/// resolved to properties by name, and every value is fetched through
/// <see cref="PropertyInfo.GetValue(object)"/>.
/// </summary>
/// <typeparam name="TRow">The row type being read.</typeparam>
/// <remarks>
/// <para>
/// This is a migration source, not a straw man. The <see cref="PropertyInfo"/> set is resolved once,
/// in the constructor, and indexed by ordinal - which is what a competent author does, and it takes
/// the per-row dictionary lookup and the per-row <c>GetProperty</c> call out of the comparison. What
/// is left is what reflection costs when it is used as well as it can be: a virtual call, an
/// argument array, and a boxed return, per value, per row.
/// </para>
/// <para>
/// Making this arm gratuitously slow would inflate the library's apparent saving and make the whole
/// report worthless. If it is beaten, it should be beaten fairly.
/// </para>
/// </remarks>
internal sealed class ReflectiveRowReader<TRow> : RowReader<TRow>
    where TRow : class
{
    private readonly PropertyInfo[] _properties;

    /// <summary>Creates the reader over a sequence of rows.</summary>
    /// <param name="rows">The rows to walk, in order.</param>
    /// <param name="columns">The columns to expose, in order; each names a property of <typeparamref name="TRow"/>.</param>
    public ReflectiveRowReader(IEnumerable<TRow> rows, IReadOnlyList<ColumnSpec> columns)
        : base(rows, columns)
    {
        _properties = new PropertyInfo[Columns.Count];

        for (var index = 0; index < Columns.Count; index++)
        {
            _properties[index] = typeof(TRow).GetProperty(Columns[index].Name)
                ?? throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{typeof(TRow).Name} has no property named {Columns[index].Name}."));
        }
    }

    // No row in this project carries a null, so the coalesce never fires during a measurement; it is
    // here because a reader that returned null where DBNull belongs would not be the thing this arm
    // is supposed to represent.
    public override object GetValue(int ordinal) => _properties[ordinal].GetValue(Current) ?? DBNull.Value;
}
