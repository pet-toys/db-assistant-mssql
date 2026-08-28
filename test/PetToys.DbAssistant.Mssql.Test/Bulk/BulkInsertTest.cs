using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Bogus;
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Extensions;
using PetToys.DbAssistant.Mssql.Test.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Bulk;

public sealed class BulkInsertTest(MsSqlFixture fixture, ITestOutputHelper output) : IClassFixture<MsSqlFixture>
{
    private const int BatchSize = 1_000_000;

    private static readonly Faker<NullableEnabledEntity> FakeNullable = CreateFaker<NullableEnabledEntity>();
    private static readonly Faker<NullableDisabledEntity> FakeNotNullable = CreateFaker<NullableDisabledEntity>();

    [DockerRequiredFact]
    public Task NullableEnabled_BulkInsert_CopiesEveryRow() =>
        RunBulkAsync(
            "#nullable_perf",
            FakeNullable.Generate(BatchSize),
            builder => builder
                .MapProperty(e => e.Int0)
                .MapProperty(e => e.Int1)
                .MapProperty(e => e.Date0)
                .MapProperty(e => e.Date1)
                .MapProperty(e => e.Str0)
                .MapProperty(e => e.Str1)
                .MapProperty(e => e.Arr0)
                .MapProperty(e => e.Arr1));

    [DockerRequiredFact]
    public Task NotNullableEnabled_BulkInsert_CopiesEveryRow() =>
        RunBulkAsync(
            "#not_nullable_perf",
            FakeNotNullable.Generate(BatchSize),
            builder => builder
                .MapProperty(e => e.Int0)
                .MapProperty(e => e.Int1)
                .MapProperty(e => e.Date0)
                .MapProperty(e => e.Date1)
                .MapProperty(e => e.Str0)
                .MapProperty(e => e.Str1, referenceNullable: true)
                .MapProperty(e => e.Arr0)
                .MapProperty(e => e.Arr1));

    [DockerRequiredFact]
    public async Task BulkInsert_RoundTrips_ValuesAndNulls()
    {
        const string tableName = "#round_trip";
        var cancellationToken = TestContext.Current.CancellationToken;
        var date0 = new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Unspecified);
        var date1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var rows = new List<NullableEnabledEntity>
        {
            new() { Int0 = 1, Int1 = null, Date0 = date0, Date1 = null, Str0 = "alpha", Str1 = null, Arr0 = [1, 2, 3], Arr1 = null },
            new() { Int0 = 2, Int1 = 20, Date0 = date0, Date1 = date1, Str0 = "beta", Str1 = "b1", Arr0 = [9], Arr1 = [8, 7] },
        };

        await using var connection = await OpenConnectionAndCreateTableAsync(tableName);
        var written = await connection.CreateBulkContext<NullableEnabledEntity>(tableName)
            .MapProperty(e => e.Int0)
            .MapProperty(e => e.Int1)
            .MapProperty(e => e.Date0)
            .MapProperty(e => e.Date1)
            .MapProperty(e => e.Str0)
            .MapProperty(e => e.Str1)
            .MapProperty(e => e.Arr0)
            .MapProperty(e => e.Arr1)
            .WriteDataAsync(rows, cancellationToken: cancellationToken);
        written.Should().Be(rows.Count);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Int0, Int1, Date0, Date1, Str0, Str1, Arr0, Arr1 FROM {tableName.QuoteName()} ORDER BY Int0;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        (await reader.ReadAsync(cancellationToken)).Should().BeTrue();
        reader.GetInt32(0).Should().Be(1);
        reader.IsDBNull(1).Should().BeTrue();
        reader.GetDateTime(2).Should().Be(date0);
        reader.IsDBNull(3).Should().BeTrue();
        reader.GetString(4).Should().Be("alpha");
        reader.IsDBNull(5).Should().BeTrue();
        ((byte[])reader[6]).Should().Equal(new byte[] { 1, 2, 3 });
        reader.IsDBNull(7).Should().BeTrue();

        (await reader.ReadAsync(cancellationToken)).Should().BeTrue();
        reader.GetInt32(0).Should().Be(2);
        reader.GetInt32(1).Should().Be(20);
        reader.GetDateTime(3).Should().Be(date1);
        reader.GetString(5).Should().Be("b1");
        ((byte[])reader[7]).Should().Equal(new byte[] { 8, 7 });

        (await reader.ReadAsync(cancellationToken)).Should().BeFalse();
    }

    [DockerRequiredFact]
    public async Task BulkInsert_AsyncSource_RoundTripsEveryRow()
    {
        const string tableName = "#async_source";
        var cancellationToken = TestContext.Current.CancellationToken;
        var rows = FakeNullable.Generate(1_000);

        await using var connection = await OpenConnectionAndCreateTableAsync(tableName);
        var written = await connection.CreateBulkContext<NullableEnabledEntity>(tableName)
            .MapProperty(e => e.Int0)
            .MapProperty(e => e.Int1)
            .MapProperty(e => e.Date0)
            .MapProperty(e => e.Date1)
            .MapProperty(e => e.Str0)
            .MapProperty(e => e.Str1)
            .MapProperty(e => e.Arr0)
            .MapProperty(e => e.Arr1)
            .WriteDataAsync(YieldAsync(rows), cancellationToken: cancellationToken);

        written.Should().Be(rows.Count);
        (await ExecuteCountAsync(connection, tableName, cancellationToken)).Should().Be(rows.Count);
    }

    [DockerRequiredFact]
    public async Task BulkInsert_EmptyAsyncSource_WritesNothing()
    {
        const string tableName = "#empty_async";
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var connection = await OpenConnectionAndCreateTableAsync(tableName);
        var written = await connection.CreateBulkContext<NullableEnabledEntity>(tableName)
            .MapProperty(e => e.Int0)
            .MapProperty(e => e.Date0)
            .MapProperty(e => e.Str0)
            .MapProperty(e => e.Arr0)
            .WriteDataAsync(YieldAsync([]), cancellationToken: cancellationToken);

        written.Should().Be(0);
        (await ExecuteCountAsync(connection, tableName, cancellationToken)).Should().Be(0);
    }

    [DockerRequiredFact]
    public async Task BulkInsert_EmptyCollection_WritesNothing()
    {
        const string tableName = "#empty";
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var connection = await OpenConnectionAndCreateTableAsync(tableName);
        var written = await connection.CreateBulkContext<NullableEnabledEntity>(tableName)
            .MapProperty(e => e.Int0)
            .MapProperty(e => e.Date0)
            .MapProperty(e => e.Str0)
            .MapProperty(e => e.Arr0)
            .WriteDataAsync([], cancellationToken: cancellationToken);

        written.Should().Be(0);
        (await ExecuteCountAsync(connection, tableName, cancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// An asynchronous producer: rows arrive one at a time, as they would from
    /// another database or a paged HTTP endpoint, so the copy is genuinely fed
    /// row by row rather than from a collection that is already in memory.
    /// </summary>
    private static async IAsyncEnumerable<NullableEnabledEntity> YieldAsync(
        IReadOnlyList<NullableEnabledEntity> rows,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return row;
        }
    }

    private static Faker<T> CreateFaker<T>()
        where T : class =>
        new Faker<T>()
            .StrictMode(true)
            .RuleFor<int>("Int0", f => f.Random.Int())
            .RuleFor<int?>("Int1", f => f.Random.Int().OrNull(f, .1f))
            .RuleFor<DateTime>("Date0", f => f.Date.Future())
            .RuleFor<DateTime?>("Date1", f => f.Date.Future().OrNull(f, .1f))
            .RuleFor<string>("Str0", f => f.Lorem.Paragraph())
            .RuleFor<string?>("Str1", f => f.Lorem.Paragraph().OrNull(f, .1f))
            .RuleFor<byte[]>("Arr0", f => f.Random.Bytes(f.Random.Number(500)))
            .RuleFor<byte[]?>("Arr1", f => f.Random.Bytes(f.Random.Number(500)).OrNull(f, .1f));

    private async Task RunBulkAsync<T>(
        string tableName,
        IReadOnlyCollection<T> data,
        Func<BulkContextBuilder<T>, BulkContextBuilder<T>> configure)
        where T : class
    {
        await using var connection = await OpenConnectionAndCreateTableAsync(tableName);
        var cancellationToken = TestContext.Current.CancellationToken;

        var watch = Stopwatch.StartNew();
        var result = await configure(connection.CreateBulkContext<T>(tableName))
            .WriteDataAsync(data, cancellationToken: cancellationToken);
        watch.Stop();

        result.Should().Be(data.Count);
        output.WriteLine("Inserted {0:N0} rows. Elapsed time: {1:N0} ms.", result, watch.ElapsedMilliseconds);
        var count = await ExecuteCountAsync(connection, tableName, cancellationToken);
        count.Should().Be(data.Count);
        await connection.CloseAsync();
    }

    private async Task<SqlConnection> OpenConnectionAndCreateTableAsync(string tableName)
    {
        var query = $"""
                     DROP TABLE IF EXISTS {tableName.QuoteName()};
                     CREATE TABLE {tableName.QuoteName()} (
                       [Int0] int NOT NULL
                      ,[Int1] int
                      ,[Date0] datetime NOT NULL
                      ,[Date1] datetime
                      ,[Str0] varchar(8000) NOT NULL
                      ,[Str1] varchar(8000)
                      ,[Arr0] varbinary(8000) NOT NULL
                      ,[Arr1] varbinary(8000)
                     );
                     """;

        var connection = (SqlConnection)fixture.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    private static async ValueTask<int> ExecuteCountAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName.QuoteName()};";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (int?)result ?? 0;
    }
}
