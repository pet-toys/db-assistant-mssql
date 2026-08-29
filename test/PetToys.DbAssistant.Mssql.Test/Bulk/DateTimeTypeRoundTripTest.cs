using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Extensions;
using PetToys.DbAssistant.Mssql.Test.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Bulk;

/// <summary>
/// The date and time family, written through the mapped bulk context into the SQL Server column
/// types it belongs in and read straight back.
/// </summary>
/// <remarks>
/// The unit tests prove only that the whitelist admits these four types. This is the one that would
/// catch the failure that matters: <c>Microsoft.Data.SqlClient</c> deciding, in some future
/// version, to convert a value on the way to the server. The library converts nothing, so any
/// difference between what went in and what came back originates below it.
/// </remarks>
public sealed class DateTimeTypeRoundTripTest(MsSqlFixture fixture) : IClassFixture<MsSqlFixture>
{
    // Neither UTC nor the build machine's zone, and not a whole number of hours, so an offset that
    // was dropped or normalised on the way through cannot coincide with the one that was written.
    private static readonly DateTimeOffset Offset = new(2026, 6, 14, 10, 15, 30, new TimeSpan(5, 30, 0));

    private static readonly TimeSpan Span = new(0, 13, 45, 30, 123);

    private static readonly DateOnly Day = new(2026, 6, 14);

    private static readonly TimeOnly Time = new(13, 45, 30, 123);

    [DockerRequiredFact]
    public async Task BulkInsert_DateTimeFamily_RoundTripsValuesAndNulls()
    {
        const string tableName = "#date_time_family";
        var cancellationToken = TestContext.Current.CancellationToken;
        var rows = BuildRows();

        await using var connection = await OpenConnectionAndCreateTableAsync(tableName);
        var written = await connection.CreateBulkContext<DateTimeEntity>(tableName)
            .MapProperty(row => row.Id)
            .MapProperty(row => row.Offset0)
            .MapProperty(row => row.Offset1)
            .MapProperty(row => row.Span0)
            .MapProperty(row => row.Span1)
            .MapProperty(row => row.Date0)
            .MapProperty(row => row.Date1)
            .MapProperty(row => row.Time0)
            .MapProperty(row => row.Time1)
            .WriteDataAsync(rows, cancellationToken: cancellationToken);
        written.Should().Be(rows.Count);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT Id, Offset0, Offset1, Span0, Span1, Date0, Date1, Time0, Time1 FROM {tableName.QuoteName()} ORDER BY Id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        (await reader.ReadAsync(cancellationToken)).Should().BeTrue();
        AssertNonNullableColumns(reader);
        // The nullable columns of the first row carry values, so they are asserted the same way.
        AssertOffset(reader.GetFieldValue<DateTimeOffset>(2));
        reader.GetFieldValue<TimeSpan>(4).Should().Be(Span);
        reader.GetDateTime(6).Should().Be(Day.ToDateTime(TimeOnly.MinValue));
        reader.GetFieldValue<TimeSpan>(8).Should().Be(Time.ToTimeSpan());

        (await reader.ReadAsync(cancellationToken)).Should().BeTrue();
        AssertNonNullableColumns(reader);
        reader.IsDBNull(2).Should().BeTrue();
        reader.IsDBNull(4).Should().BeTrue();
        reader.IsDBNull(6).Should().BeTrue();
        reader.IsDBNull(8).Should().BeTrue();

        (await reader.ReadAsync(cancellationToken)).Should().BeFalse();
    }

    private static List<DateTimeEntity> BuildRows() =>
    [
        new()
        {
            Id = 1,
            Offset0 = Offset,
            Offset1 = Offset,
            Span0 = Span,
            Span1 = Span,
            Date0 = Day,
            Date1 = Day,
            Time0 = Time,
            Time1 = Time,
        },
        new()
        {
            Id = 2,
            Offset0 = Offset,
            Span0 = Span,
            Date0 = Day,
            Time0 = Time,
        },
    ];

    /// <summary>
    /// Reads the four non-nullable columns back and asserts each against what was written.
    /// </summary>
    /// <remarks>
    /// <c>DateOnly</c> and <c>TimeOnly</c> are read as <see cref="DateTime"/> and
    /// <see cref="TimeSpan"/> because that is what the provider returns for a <c>date</c> and a
    /// <c>time</c> column. It says nothing about this change - the library only writes - but a test
    /// written as though a column gives back the CLR type it was handed fails with an
    /// <c>InvalidCastException</c> and reads like a defect in the mapping.
    /// </remarks>
    private static void AssertNonNullableColumns(SqlDataReader reader)
    {
        AssertOffset(reader.GetFieldValue<DateTimeOffset>(1));
        reader.GetFieldValue<TimeSpan>(3).Should().Be(Span);
        reader.GetDateTime(5).Should().Be(Day.ToDateTime(TimeOnly.MinValue));
        reader.GetFieldValue<TimeSpan>(7).Should().Be(Time.ToTimeSpan());
    }

    /// <summary>
    /// Asserts a <see cref="DateTimeOffset"/> on its offset as well as its instant.
    /// </summary>
    /// <remarks>
    /// Never <c>==</c>, and so never <c>Should().Be()</c>: two <see cref="DateTimeOffset"/> values
    /// denoting the same instant at different offsets compare equal, so a round trip that silently
    /// normalised everything to UTC would pass such an assertion while destroying the one piece of
    /// information the type exists to carry. The two halves are asserted separately as well so that
    /// a failure says which of them moved.
    /// </remarks>
    private static void AssertOffset(DateTimeOffset read)
    {
        read.Offset.Should().Be(Offset.Offset);
        read.DateTime.Should().Be(Offset.DateTime);
        read.EqualsExact(Offset).Should().BeTrue();
    }

    private async Task<SqlConnection> OpenConnectionAndCreateTableAsync(string tableName)
    {
        var query = $"""
                     DROP TABLE IF EXISTS {tableName.QuoteName()};
                     CREATE TABLE {tableName.QuoteName()} (
                       [Id] int NOT NULL
                      ,[Offset0] datetimeoffset(7) NOT NULL
                      ,[Offset1] datetimeoffset(7)
                      ,[Span0] time(7) NOT NULL
                      ,[Span1] time(7)
                      ,[Date0] date NOT NULL
                      ,[Date1] date
                      ,[Time0] time(7) NOT NULL
                      ,[Time1] time(7)
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
