using System.Diagnostics;
using System.Threading.Tasks;
using Bogus;
using FluentAssertions;
using PetToys.DbAssistant.Mssql.Extensions;
using PetToys.DbAssistant.Mssql.Test.Accessors;
using Xunit;
using Xunit.Abstractions;

namespace PetToys.DbAssistant.Mssql.Test;

[Trait("Category", "Integration")]
public sealed class BulkInsertTest(ITestOutputHelper output) : DatabaseTestBase
{
    private const int BatchSize = 1_000;
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

    [Fact]
    public async Task NullableEnabled_Test()
    {
        var data = FakeNullable.Generate(BatchSize);
        const string tableName = "nullable_test";
        await using var connection = await ReCreateTableAsync(tableName);
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
            .WriteDataAsync(data);
        watch.Stop();
        result.Should().Be(data.Count);
        output.WriteLine("Inserted {0:N0} rows. Elapsed time: {1:N0} ms.", result, watch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task NotNullableEnabled_Test()
    {
        var data = FakeNotNullable.Generate(BatchSize);
        const string tableName = "not_nullable_test";
        await using var connection = await ReCreateTableAsync(tableName);
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
            .WriteDataAsync(data);
        watch.Stop();
        result.Should().Be(data.Count);
        output.WriteLine("Inserted {0:N0} rows. Elapsed time: {1:N0} ms.", result, watch.ElapsedMilliseconds);
    }
}
