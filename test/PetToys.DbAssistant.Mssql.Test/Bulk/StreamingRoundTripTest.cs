using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Extensions;
using PetToys.DbAssistant.Mssql.Test.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Bulk;

/// <summary>
/// <see cref="SqlBulkOptions.EnableStreaming"/> changes how a value reaches the server, not what
/// arrives. That is the claim a caller relies on when they turn it on, and the reason the default
/// could be moved without it being a breaking change to anybody's data.
/// </summary>
/// <remarks>
/// The documents are deliberately over the eight-thousand-byte in-row threshold. A shorter value is
/// stored with the rest of the row and never takes the large-object path, which is the path this
/// flag changes, so a test built on short values would compare a setting against itself and pass
/// without exercising anything.
/// </remarks>
public sealed class StreamingRoundTripTest(MsSqlFixture fixture) : IClassFixture<MsSqlFixture>
{
    private const int OffRowDocumentLength = 20_000;

    [DockerRequiredFact]
    public async Task BulkInsert_WithAndWithoutStreaming_LandsIdenticalRows()
    {
        const string tableName = "#streaming_round_trip";
        var cancellationToken = TestContext.Current.CancellationToken;
        var rows = BuildRows();

        await using var connection = await OpenConnectionAndCreateTableAsync(tableName);

        var withDefault = await CopyAndReadBackAsync(connection, tableName, rows, null, cancellationToken);
        await TruncateAsync(connection, tableName, cancellationToken);
        var withStreaming = await CopyAndReadBackAsync(
            connection,
            tableName,
            rows,
            options => options.EnableStreaming = true,
            cancellationToken);

        withDefault.Should().Equal(rows.Select(row => (row.Id, row.Document)));
        withStreaming.Should().Equal(withDefault);
    }

    private static List<LargeDocument> BuildRows() =>
    [
        new() { Id = 1, Document = new string('a', OffRowDocumentLength) },
        new() { Id = 2, Document = new string('b', OffRowDocumentLength + 1) },
        new() { Id = 3, Document = new string('c', OffRowDocumentLength * 2) },
    ];

    private static async Task<List<(int Id, string Document)>> CopyAndReadBackAsync(
        SqlConnection connection,
        string tableName,
        IReadOnlyList<LargeDocument> rows,
        Action<SqlBulkOptions>? optionsBuilder,
        CancellationToken cancellationToken)
    {
        var written = await connection.CreateBulkContext<LargeDocument>(tableName)
            .MapProperty(row => row.Id)
            .MapProperty(row => row.Document)
            .WriteDataAsync(rows, optionsBuilder, cancellationToken: cancellationToken);
        written.Should().Be(rows.Count);

        var readBack = new List<(int Id, string Document)>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Id, Document FROM {tableName.QuoteName()} ORDER BY Id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            readBack.Add((reader.GetInt32(0), reader.GetString(1)));
        }

        return readBack;
    }

    private static async Task TruncateAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"TRUNCATE TABLE {tableName.QuoteName()};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqlConnection> OpenConnectionAndCreateTableAsync(string tableName)
    {
        var query = $"""
                     DROP TABLE IF EXISTS {tableName.QuoteName()};
                     CREATE TABLE {tableName.QuoteName()} (
                       [Id] int NOT NULL
                      ,[Document] varchar(max) NOT NULL
                     );
                     """;

        var connection = (SqlConnection)fixture.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        await command.ExecuteNonQueryAsync();
        return connection;
    }
}
