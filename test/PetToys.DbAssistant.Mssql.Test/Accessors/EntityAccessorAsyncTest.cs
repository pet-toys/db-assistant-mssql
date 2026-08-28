using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using PetToys.DbAssistant.Mssql.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

/// <summary>
/// Coverage for the asynchronous side of the hand-written <c>DbDataReader</c>:
/// the same projection as <see cref="EntityAccessorTest"/>, driven through
/// <c>ReadAsync</c> over an <c>IAsyncEnumerable</c>. Columns under test:
/// <c>0:Int0</c> (int, not null), <c>1:Str1</c> (string, nullable),
/// <c>2:Arr0</c> (byte[], not null), <c>3:Date0</c> (DateTime, not null).
/// </summary>
public sealed class EntityAccessorAsyncTest
{
    private static readonly DateTime SampleDate = new(2026, 8, 28, 1, 2, 3, DateTimeKind.Unspecified);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NullableEnabledEntity FullRow => new()
    {
        Int0 = 7,
        Str1 = "hello",
        Arr0 = [1, 2, 3, 4],
        Date0 = SampleDate,
    };

    private static NullableEnabledEntity NullStringRow => new()
    {
        Int0 = 8,
        Str1 = null,
        Arr0 = [9],
        Date0 = SampleDate,
    };

    [Fact]
    public async Task ReadAsync_IteratesEveryRowThenStops()
    {
        var reader = await CreateReaderAsync(FullRow, NullStringRow);
        await using (reader)
        {
            (await reader.ReadAsync(Token)).Should().BeTrue();
            (await reader.ReadAsync(Token)).Should().BeTrue();
            (await reader.ReadAsync(Token)).Should().BeFalse();
            (await reader.ReadAsync(Token)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task ReadAsync_ProjectsValuesLikeTheSynchronousPath()
    {
        var reader = await CreateReaderAsync(FullRow, NullStringRow);
        await using (reader)
        {
            (await reader.ReadAsync(Token)).Should().BeTrue();
            reader[0].Should().Be(7);
            reader["Str1"].Should().Be("hello");
            reader.GetDateTime(3).Should().Be(SampleDate);
            reader["Arr0"].Should().BeOfType<byte[]>().Which.Should().Equal(new byte[] { 1, 2, 3, 4 });

            (await reader.ReadAsync(Token)).Should().BeTrue();
            reader.GetInt32(0).Should().Be(8);
            reader.IsDBNull(1).Should().BeTrue();
            reader[1].Should().Be(DBNull.Value);
        }
    }

    [Fact]
    public async Task Metadata_MatchesTheSynchronousReader()
    {
        var reader = await CreateReaderAsync(FullRow);
        await using (reader)
        {
            reader.FieldCount.Should().Be(4);
            reader.GetName(0).Should().Be("Int0");
            reader.GetOrdinal("Date0").Should().Be(3);
            reader.GetFieldType(2).Should().Be<byte[]>();
            reader.GetSchemaTable().Rows[1]["ColumnName"].Should().Be("Str1");
        }
    }

    [Fact]
    public async Task HasRows_IsFalse_ForEmptyAsyncSource()
    {
        var reader = await CreateReaderAsync();
        await using (reader)
        {
            reader.HasRows.Should().BeFalse();
            (await reader.ReadAsync(Token)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task HasRows_IsTrue_BeforeTheFirstReadAsync()
    {
        var reader = await CreateReaderAsync(FullRow);
        await using (reader)
        {
            reader.HasRows.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_ThrowsAndDoesNotAdvance()
    {
        var source = new TrackingAsyncSource<NullableEnabledEntity>(FullRow, NullStringRow);
        var reader = await EntityAccessor<NullableEnabledEntity>.CreateAsync(source, CreateAccessors(), Token);
        await using (reader)
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var pulledBefore = source.Pulled;

            var act = async () => await reader.ReadAsync(cancellation.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            source.Pulled.Should().Be(pulledBefore);
        }
    }

    [Fact]
    public async Task Read_OnAsyncBackedReader_ThrowsNotSupportedAndDoesNotAdvance()
    {
        var reader = await CreateReaderAsync(FullRow, NullStringRow);
        await using (reader)
        {
            var act = () => reader.Read();

            act.Should().Throw<NotSupportedException>();
            (await reader.ReadAsync(Token)).Should().BeTrue();
            reader.GetInt32(0).Should().Be(7);
        }
    }

    [Fact]
    public async Task ReadAsync_OnSyncBackedReader_StillReads()
    {
        using var reader = new EntityAccessor<NullableEnabledEntity>([FullRow], CreateAccessors());

        (await reader.ReadAsync(Token)).Should().BeTrue();
        reader.GetInt32(0).Should().Be(7);
        (await reader.ReadAsync(Token)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_TakesOneEnumerator_AndPassesTheToken()
    {
        var source = new TrackingAsyncSource<NullableEnabledEntity>(FullRow);
        using var cancellation = new CancellationTokenSource();

        var reader = await EntityAccessor<NullableEnabledEntity>.CreateAsync(source, CreateAccessors(), cancellation.Token);
        await using (reader)
        {
            while (await reader.ReadAsync(Token))
            {
                // Drain the source.
            }
        }

        source.EnumeratorCount.Should().Be(1);
        source.Token.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task CreateAsync_PullsOnlyTheLookaheadRow()
    {
        var source = new TrackingAsyncSource<NullableEnabledEntity>(FullRow, NullStringRow);

        var reader = await EntityAccessor<NullableEnabledEntity>.CreateAsync(source, CreateAccessors(), Token);
        await using (reader)
        {
            source.Pulled.Should().Be(1);
        }
    }

    [Fact]
    public async Task DisposeAsync_ClosesTheReaderAndDisposesTheSource()
    {
        var source = new TrackingAsyncSource<NullableEnabledEntity>(FullRow);
        var reader = await EntityAccessor<NullableEnabledEntity>.CreateAsync(source, CreateAccessors(), Token);

        reader.IsClosed.Should().BeFalse();
        await reader.DisposeAsync();

        reader.IsClosed.Should().BeTrue();
        source.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_OnAsyncBackedReader_StillDisposesTheSource()
    {
        var source = new TrackingAsyncSource<NullableEnabledEntity>(FullRow);
        var reader = await EntityAccessor<NullableEnabledEntity>.CreateAsync(source, CreateAccessors(), Token);

        reader.Dispose();

        reader.IsClosed.Should().BeTrue();
        source.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_FaultingSource_PropagatesAndDisposesTheEnumerator()
    {
        var source = new FaultingAsyncSource();

        var act = async () =>
            await EntityAccessor<NullableEnabledEntity>.CreateAsync(source, CreateAccessors(), Token);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("producer failed");
        source.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_PassesTheTokenIntoAnIteratorProducer()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () =>
            await EntityAccessor<NullableEnabledEntity>.CreateAsync(CancellableRows(FullRow), CreateAccessors(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async IAsyncEnumerable<NullableEnabledEntity> AsyncRows(NullableEnabledEntity[] rows)
    {
        foreach (var row in rows)
        {
            await Task.Yield();
            yield return row;
        }
    }

    private static async IAsyncEnumerable<NullableEnabledEntity> CancellableRows(
        NullableEnabledEntity row,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return row;
    }

    private static List<IPropertyAccessor<NullableEnabledEntity>> CreateAccessors() =>
    [
        new PropertyAccessor<NullableEnabledEntity, int>(e => e.Int0),
        new PropertyAccessor<NullableEnabledEntity, string?>(e => e.Str1),
        new PropertyAccessor<NullableEnabledEntity, byte[]>(e => e.Arr0),
        new PropertyAccessor<NullableEnabledEntity, DateTime>(e => e.Date0),
    ];

    private static ValueTask<EntityAccessor<NullableEnabledEntity>> CreateReaderAsync(params NullableEnabledEntity[] rows) =>
        EntityAccessor<NullableEnabledEntity>.CreateAsync(AsyncRows(rows), CreateAccessors(), Token);

    /// <summary>A source whose first row already fails, to exercise the factory's cleanup.</summary>
    private sealed class FaultingAsyncSource : IAsyncEnumerable<NullableEnabledEntity>, IAsyncEnumerator<NullableEnabledEntity>
    {
        public NullableEnabledEntity Current => null!;

        public bool Disposed { get; private set; }

        public IAsyncEnumerator<NullableEnabledEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;

        public async ValueTask<bool> MoveNextAsync()
        {
            await Task.Yield();
            throw new InvalidOperationException("producer failed");
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return default;
        }
    }
}
