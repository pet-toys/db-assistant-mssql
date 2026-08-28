using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

/// <summary>
/// An <see cref="IAsyncEnumerable{T}"/> that records how it was consumed: how
/// many enumerators were taken, which token they were given, how many rows were
/// pulled, and whether the enumerator was disposed. Rows are yielded through
/// <see cref="Task.Yield"/>, so the consumer really does resume asynchronously.
/// </summary>
internal sealed class TrackingAsyncSource<T>(params T[] items) : IAsyncEnumerable<T>
{
    private readonly T[] _items = items;

    public int EnumeratorCount { get; private set; }

    public CancellationToken Token { get; private set; }

    public int Pulled { get; private set; }

    public bool Disposed { get; private set; }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        EnumeratorCount++;
        Token = cancellationToken;
        return new Enumerator(this);
    }

    private sealed class Enumerator(TrackingAsyncSource<T> owner) : IAsyncEnumerator<T>
    {
        private int _index = -1;

        public T Current => owner._items[_index];

        public async ValueTask<bool> MoveNextAsync()
        {
            await Task.Yield();
            if (_index + 1 >= owner._items.Length) return false;
            _index++;
            owner.Pulled++;
            return true;
        }

        public ValueTask DisposeAsync()
        {
            owner.Disposed = true;
            return default;
        }
    }
}
