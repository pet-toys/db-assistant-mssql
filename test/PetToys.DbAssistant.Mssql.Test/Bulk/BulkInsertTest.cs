using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Extensions;
using PetToys.DbAssistant.Mssql.Test.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Bulk;

public sealed class BulkInsertTest(MsSqlFixture fixture, ITestOutputHelper output) : IClassFixture<MsSqlFixture>
{
    private const int BatchSize = 1_000_000;

    private static readonly Faker<NullableEnabledEntity> FakeNullable = new Faker<NullableEnabledEntity>()
        .StrictMode(true)
        .RuleFor(e => e.Int0, f => f.Random.Int())
        .RuleFor(e => e.Int1, f => f.Random.Int().OrNull(f, .1f))
        .RuleFor(e => e.Date0, f => f.Date.Future())
        .RuleFor(e => e.Date1, f => f.Date.Future().OrNull(f, 0.1f))
        .RuleFor(e => e.Str0, f => f.Lorem.Paragraph())
        .RuleFor(e => e.Str1, f => f.Lorem.Paragraph().OrNull(f, .1f))
        .RuleFor(e => e.Arr0, f => f.Random.Bytes(f.Random.Number(500)))
        .RuleFor(e => e.Arr1, f => f.Random.Bytes(f.Random.Number(500)).OrNull(f, .1f));

    private static readonly Faker<NullableDisabledEntity> FakeNotNullable = new Faker<NullableDisabledEntity>()
        .StrictMode(true)
        .RuleFor(e => e.Int0, f => f.Random.Int())
        .RuleFor(e => e.Int1, f => f.Random.Int().OrNull(f, .1f))
        .RuleFor(e => e.Date0, f => f.Date.Future())
        .RuleFor(e => e.Date1, f => f.Date.Future().OrNull(f, 0.1f))
        .RuleFor(e => e.Str0, f => f.Lorem.Paragraph())
        .RuleFor(e => e.Str1, f => f.Lorem.Paragraph().OrNull(f, .1f))
        .RuleFor(e => e.Arr0, f => f.Random.Bytes(f.Random.Number(500)))
        .RuleFor(e => e.Arr1, f => f.Random.Bytes(f.Random.Number(500)).OrNull(f, .1f));

    [LinuxOnlyFact]
    public async Task NullableEnabled_Test()
    {
        var data = FakeNullable.Generate(BatchSize);
        const string tableName = "#nullable_test";
        await using var connection = await OpenConnectionAndCreateTableAsync(tableName);
        var watch = Stopwatch.StartNew();
        var result = await connection.CreateBulkContext<NullableEnabledEntity>(tableName)
            .MapProperty(e => e.Int0)
            .MapProperty(e => e.Int1)
            .MapProperty(e => e.Date0)
            .MapProperty(e => e.Date1)
            .MapProperty(e => e.Str0)
            .MapProperty(e => e.Str1)
            .MapProperty(e => e.Arr0)
            .MapProperty(e => e.Arr1)
            .WriteDataAsync(data, cancellationToken: TestContext.Current.CancellationToken);
        watch.Stop();
        result.Should().Be(data.Count);
        output.WriteLine("Inserted {0:N0} rows. Elapsed time: {1:N0} ms.", result, watch.ElapsedMilliseconds);
        var count = await ExecuteCountAsync(connection, tableName, TestContext.Current.CancellationToken);
        count.Should().Be(data.Count);
        await connection.CloseAsync();
    }

    [LinuxOnlyFact]
    public async Task NotNullableEnabled_Test()
    {
        var data = FakeNotNullable.Generate(BatchSize);
        const string tableName = "#not_nullable_test";
        await using var connection = await OpenConnectionAndCreateTableAsync(tableName);
        var watch = Stopwatch.StartNew();
        var result = await connection.CreateBulkContext<NullableDisabledEntity>(tableName)
            .MapProperty(e => e.Int0)
            .MapProperty(e => e.Int1)
            .MapProperty(e => e.Date0)
            .MapProperty(e => e.Date1)
            .MapProperty(e => e.Str0)
            .MapProperty(e => e.Str1, referenceNullable: true)
            .MapProperty(e => e.Arr0)
            .MapProperty(e => e.Arr1)
            .WriteDataAsync(data, cancellationToken: TestContext.Current.CancellationToken);
        watch.Stop();
        result.Should().Be(data.Count);
        output.WriteLine("Inserted {0:N0} rows. Elapsed time: {1:N0} ms.", result, watch.ElapsedMilliseconds);
        var count = await ExecuteCountAsync(connection, tableName, TestContext.Current.CancellationToken);
        count.Should().Be(data.Count);
        await connection.CloseAsync();
    }

    private async Task<SqlConnection> OpenConnectionAndCreateTableAsync(string tableName)
    {
        var query = ($"""
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
                      """);

        var connection = (SqlConnection)fixture.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    private static async ValueTask<int> ExecuteCountAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken = default)
    {
        var query = $"SELECT COUNT(*) FROM {tableName};";
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (int?)result ?? 0;
    }
}
