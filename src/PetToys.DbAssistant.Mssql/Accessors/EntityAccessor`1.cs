using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace PetToys.DbAssistant.Mssql.Accessors;

internal sealed class EntityAccessor<TEntity> : DbDataReader
    where TEntity : class
{
    private readonly ImmutableArray<IPropertyAccessor<TEntity>> _accessors;
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
    private IAsyncEnumerator<TEntity>? _asyncSource;
    private TEntity? _current;
    private bool _hasCurrent;
    private TEntity? _lookahead;
    private bool _hasLookahead;
    private bool _hasRows;
    private bool _resultEnded;

    public EntityAccessor(IEnumerable<TEntity> source, List<IPropertyAccessor<TEntity>> accessors)
        : this(accessors)
    {
        _source = source.GetEnumerator();
        if (_source.MoveNext())
        {
            _lookahead = _source.Current;
            _hasLookahead = true;
            _hasRows = true;
        }
    }

    private EntityAccessor(List<IPropertyAccessor<TEntity>> accessors)
    {
        _accessors = accessors.ToImmutableArray();
        var namedIndexes = new Dictionary<string, int>(accessors.Count);
        for (var i = 0; i < accessors.Count; i++)
        {
            namedIndexes.Add(accessors[i].PropertyName, i);
            _schemaTable.Rows.Add(i, accessors[i].PropertyName, accessors[i].ClrType, -1, accessors[i].IsNullable);
        }

        _namedIndexes = ImmutableDictionary.CreateRange(namedIndexes);
    }

    /// <summary>
    /// Creates a reader over an asynchronous source. The first row is fetched
    /// here, which a constructor cannot do, so that <see cref="HasRows"/> is
    /// answerable before the first <see cref="ReadAsync(CancellationToken)"/>,
    /// exactly as it is for a synchronous source.
    /// </summary>
    public static async ValueTask<EntityAccessor<TEntity>> CreateAsync(
        IAsyncEnumerable<TEntity> source,
        List<IPropertyAccessor<TEntity>> accessors,
        CancellationToken cancellationToken)
    {
        var reader = new EntityAccessor<TEntity>(accessors);
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        reader._asyncSource = enumerator;
        try
        {
            if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                reader._lookahead = enumerator.Current;
                reader._hasLookahead = true;
                reader._hasRows = true;
            }
        }
        catch
        {
            await reader.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return reader;
    }

    public override object this[int ordinal] => _accessors[ordinal].GetValue(Current()) ?? DBNull.Value;

    public override object this[string name] => _accessors[GetOrdinal(name)].GetValue(Current()) ?? DBNull.Value;

    public override int Depth => 0;

    public override int FieldCount => _accessors.Length;

    public override bool HasRows => _hasRows && !_resultEnded;

    public override bool IsClosed => _source is null && _asyncSource is null;

    public override int RecordsAffected => 0;

    public override async ValueTask DisposeAsync()
    {
        var asyncSource = _asyncSource;
        _asyncSource = null;
        ResetRow();
        if (asyncSource is not null) await asyncSource.DisposeAsync().ConfigureAwait(false);

        // Disposes the synchronous source, if that is the one this reader holds.
        await base.DisposeAsync().ConfigureAwait(false);
    }

    public override bool GetBoolean(int ordinal) => (bool)this[ordinal];

    public override byte GetByte(int ordinal) => (byte)this[ordinal];

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var src = (byte[])this[ordinal];
        if (buffer is null) return src.LongLength;
        var diff = src.LongLength - dataOffset;
        if (diff <= 0L) return 0L;
        var count = Math.Min(length, diff);
        Buffer.BlockCopy(src, (int)dataOffset, buffer, bufferOffset, (int)count);
        return count;
    }

    public override char GetChar(int ordinal) => (char)this[ordinal];

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        var str = (string)this[ordinal];
        if (buffer is null) return str.Length;
        var diff = str.Length - dataOffset;
        if (diff <= 0L) return 0L;
        var count = Math.Min(length, diff);
        str.CopyTo((int)dataOffset, buffer, bufferOffset, (int)count);
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

    public override int GetOrdinal(string name)
    {
        if (_namedIndexes.TryGetValue(name, out var ordinal)) return ordinal;

        // The IDataReader.GetOrdinal contract mandates IndexOutOfRangeException for an unknown name.
#pragma warning disable CA2201
        throw new IndexOutOfRangeException(name);
#pragma warning restore CA2201
    }

    public override DataTable GetSchemaTable() => _schemaTable.Copy();

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
        _resultEnded = true;
        ResetRow();
        return false;
    }

    public override bool Read()
    {
        // An asynchronous source has no correct synchronous answer, and blocking
        // on it is the deadlock this library's ConfigureAwait rule exists to avoid.
        if (_asyncSource is not null) throw new NotSupportedException("This reader is backed by an asynchronous source; call ReadAsync instead.");
        if (!TryStartRow()) return false;

        if (_source!.MoveNext())
        {
            _lookahead = _source.Current;
        }
        else
        {
            _lookahead = null;
            _hasLookahead = false;
        }

        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        var source = _asyncSource;

        // A synchronous source keeps the base behaviour: it has nothing to await.
        if (source is null) return await base.ReadAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryStartRow()) return false;

        if (await source.MoveNextAsync().ConfigureAwait(false))
        {
            _lookahead = source.Current;
        }
        else
        {
            _lookahead = null;
            _hasLookahead = false;
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        Shutdown();
    }

    private TEntity Current() =>
        _hasCurrent ? _current! : throw new InvalidOperationException("No current row; call Read first.");

    private bool TryStartRow()
    {
        if (_resultEnded || !_hasLookahead)
        {
            ResetRow();
            return false;
        }

        _current = _lookahead;
        _hasCurrent = true;
        return true;
    }

    private void ResetRow()
    {
        _hasCurrent = false;
        _current = null;
    }

    private void Shutdown()
    {
        ResetRow();
        _hasLookahead = false;
        _lookahead = null;
        _source?.Dispose();
        _source = null;
        var asyncSource = _asyncSource;
        _asyncSource = null;
        if (asyncSource is null) return;

        // Reached only when an asynchronously backed reader is disposed
        // synchronously: the library itself always awaits DisposeAsync, and
        // leaking the enumerator would be worse than the blocking wait. The
        // disposal runs on the thread pool so that the producer's own awaits
        // resume there instead of on a synchronization context this thread is
        // blocking. ConfigureAwait(false) would not do it: it configures a
        // continuation, and a blocking wait registers none.
        Task.Run(() => asyncSource.DisposeAsync().AsTask()).GetAwaiter().GetResult();
    }
}
