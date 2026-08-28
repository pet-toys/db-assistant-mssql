using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The plumbing every hand-written reader arm needs, so that the arms differ in exactly one thing:
/// how a value is fetched off a row.
/// </summary>
/// <typeparam name="TRow">The row type being read.</typeparam>
/// <remarks>
/// <para>
/// Rows are walked through an <see cref="IEnumerator{T}"/> rather than indexed out of a list, because
/// that is how the library walks them. Indexing would be a legitimate thing for a hand-written
/// reader to do and it would also fold an iteration difference into a comparison that is supposed to
/// be about value access alone.
/// </para>
/// <para>
/// Only a handful of these members are on <see cref="Microsoft.Data.SqlClient.SqlBulkCopy"/>'s path -
/// <see cref="FieldCount"/>, <see cref="Read"/>, <c>GetValue</c>, <see cref="GetName"/> and
/// <see cref="GetOrdinal"/>. The typed getters are implemented in terms of
/// <c>GetValue</c> for completeness; nothing in a measured region calls them.
/// </para>
/// </remarks>
internal abstract class RowReader<TRow> : DbDataReader
    where TRow : class
{
    private readonly IEnumerator<TRow> _rows;
    private readonly bool _hasRows;
    private bool _primed;
    private bool _closed;

    /// <summary>Creates the reader over a sequence of rows.</summary>
    /// <param name="rows">The rows to walk, in order.</param>
    /// <param name="columns">The columns to expose, in order.</param>
    protected RowReader(IEnumerable<TRow> rows, IReadOnlyList<ColumnSpec> columns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);
        Columns = columns;
        _rows = rows.GetEnumerator();

        // The library keeps HasRows honest with a one-row look-ahead, so this does too. Nothing on
        // the bulk copy path asks for it, but a reader that lied here would be a reader nobody else
        // could reuse.
        _hasRows = _rows.MoveNext();
        _primed = _hasRows;
    }

    /// <summary>The columns this reader exposes, in ordinal order.</summary>
    protected IReadOnlyList<ColumnSpec> Columns { get; }

    /// <summary>The row the reader is positioned on.</summary>
    protected TRow Current => _rows.Current;

    public override int FieldCount => Columns.Count;

    public override bool HasRows => _hasRows;

    public override bool IsClosed => _closed;

    public override int Depth => 0;

    public override int RecordsAffected => -1;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        if (_closed) return false;

        // The constructor already advanced onto the first row so that HasRows could be answered
        // without lying. The first Read therefore consumes that row rather than skipping it.
        if (_primed)
        {
            _primed = false;
            return true;
        }

        return _rows.MoveNext();
    }

    public override bool NextResult() => false;

    public override string GetName(int ordinal) => Columns[ordinal].Name;

    public override Type GetFieldType(int ordinal) => Columns[ordinal].ClrType;

    public override string GetDataTypeName(int ordinal) => Columns[ordinal].DataType;

    public override int GetOrdinal(string name)
    {
        for (var index = 0; index < Columns.Count; index++)
        {
            if (string.Equals(Columns[index].Name, name, StringComparison.OrdinalIgnoreCase)) return index;
        }

        throw new ArgumentOutOfRangeException(nameof(name), name, $"{name} is not a column of {GetType().Name}.");
    }

    public override bool IsDBNull(int ordinal) => GetValue(ordinal) is DBNull;

    public override int GetValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var count = Math.Min(values.Length, FieldCount);

        for (var index = 0; index < count; index++)
        {
            values[index] = GetValue(index);
        }

        return count;
    }

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);

    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);

    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        CopyInto((byte[])GetValue(ordinal), dataOffset, buffer, bufferOffset, length);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        CopyInto(((string)GetValue(ordinal)).ToCharArray(), dataOffset, buffer, bufferOffset, length);

    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_closed)
        {
            _closed = true;
            _rows.Dispose();
        }

        base.Dispose(disposing);
    }

    private static long CopyInto<TElement>(TElement[] source, long dataOffset, TElement[]? buffer, int bufferOffset, int length)
    {
        if (buffer is null) return source.Length;

        var available = (int)Math.Min(source.Length - dataOffset, length);
        if (available <= 0) return 0;

        Array.Copy(source, dataOffset, buffer, bufferOffset, available);
        return available;
    }

    /// <summary>
    /// Formats an ordinal that is not a column of this reader. The one place the readers need a
    /// message, kept here so the arms do not differ even in their exceptions.
    /// </summary>
    /// <param name="ordinal">The ordinal that was asked for.</param>
    protected string UnknownOrdinal(int ordinal) =>
        string.Create(CultureInfo.InvariantCulture, $"Ordinal {ordinal} is not a column of {GetType().Name}.");
}
