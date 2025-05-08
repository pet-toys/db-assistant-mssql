using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace PetToys.DbAssistant.Mssql.Accessors;

internal sealed class EntityAccessor<TEntity> : DbDataReader
    where TEntity : class
{
    private readonly ImmutableList<IPropertyAccessor<TEntity>> _accessors;
    private readonly ImmutableDictionary<string, int> _namedIndexes;

    private readonly DataTable _schemaTable = new()
    {
        Columns =
        {
            { "ColumnOrdinal", typeof(int) },
            { "ColumnName", typeof(string) },
            { "DataType", typeof(Type) },
            { "ColumnSize", typeof(int) },
            { "AllowDBNull", typeof(bool) },
        },
    };

    private IEnumerator<TEntity>? _source;
    private TEntity? _current;
    private bool _canRead = true;

    public EntityAccessor(IEnumerable<TEntity> source, List<IPropertyAccessor<TEntity>> accessors)
    {
        _source = source.GetEnumerator();
        _accessors = ImmutableList.Create(accessors.ToArray());
        var namedIndexes = new Dictionary<string, int>(accessors.Count);
        for (var i = 0; i < accessors.Count; i++)
        {
            namedIndexes.Add(accessors[i].PropertyName, i);
            _schemaTable.Rows.Add(i, accessors[i].ColumnName, accessors[i].ClrType, -1, accessors[i].IsNullable);
        }

        _namedIndexes = ImmutableDictionary.CreateRange(namedIndexes);
    }

    public override object this[int ordinal] => _accessors[ordinal].GetValue(_current!) ?? DBNull.Value;

    public override object this[string name] => _accessors[GetOrdinal(name)].GetValue(_current!) ?? DBNull.Value;

    public override int Depth => 0;

    public override int FieldCount => _accessors.Count;

    public override bool HasRows => _canRead;

    public override bool IsClosed => _source is null;

    public override int RecordsAffected => 0;

    public override bool GetBoolean(int ordinal) => (bool)this[ordinal];

    public override byte GetByte(int ordinal) => (byte)this[ordinal];

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var src = (byte[])this[ordinal];
        var diff = src.LongLength - dataOffset;
        if (diff <= 0L) return 0L;
        var count = Math.Min(length, diff);
        Buffer.BlockCopy(src, (int)dataOffset, buffer!, bufferOffset, (int)count);
        return count;
    }

    public override char GetChar(int ordinal) => (char)this[ordinal];

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        var str = (string)this[ordinal];
        var diff = str.Length - (int)dataOffset;
        if (diff <= 0) return 0;
        var count = Math.Min(length, diff);
        str.CopyTo((int)dataOffset, buffer!, bufferOffset, count);
        return count;
    }

    public override string GetDataTypeName(int ordinal) => _accessors[ordinal].EffectiveType.Name;

    public override DateTime GetDateTime(int ordinal) => (DateTime)this[ordinal];

    public override decimal GetDecimal(int ordinal) => (decimal)this[ordinal];

    public override double GetDouble(int ordinal) => (double)this[ordinal];

    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal) => _accessors[ordinal].EffectiveType;

    public override float GetFloat(int ordinal) => (float)this[ordinal];

    public override Guid GetGuid(int ordinal) => (Guid)this[ordinal];

    public override short GetInt16(int ordinal) => (short)this[ordinal];

    public override int GetInt32(int ordinal) => (int)this[ordinal];

    public override long GetInt64(int ordinal) => (long)this[ordinal];

    public override string GetName(int ordinal) => _accessors[ordinal].PropertyName;

    public override int GetOrdinal(string name) => _namedIndexes[name];

    public override DataTable GetSchemaTable() => _schemaTable;

    public override string GetString(int ordinal) => (string)this[ordinal];

    public override object GetValue(int ordinal) => this[ordinal];

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = this[i];
        }

        return count;
    }

    public override bool IsDBNull(int ordinal) => this[ordinal] is DBNull;

    public override bool NextResult()
    {
        _canRead = false;
        return false;
    }

    public override bool Read()
    {
        if (_canRead)
        {
            if (_source?.MoveNext() == true)
            {
                _current = _source.Current;
                return true;
            }

            _canRead = false;
        }

        _current = null;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        Shutdown();
    }

    private void Shutdown()
    {
        _canRead = false;
        _current = null;
        _source?.Dispose();
        _source = null;
    }
}
