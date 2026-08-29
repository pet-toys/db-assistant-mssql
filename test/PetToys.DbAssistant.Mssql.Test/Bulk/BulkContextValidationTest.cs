using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Extensions;
using PetToys.DbAssistant.Mssql.Test.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Bulk;

public sealed class BulkContextValidationTest
{
    [Fact]
    public void CreateBulkContext_NullConnection_ThrowsForConnection()
    {
        SqlConnection connection = null!;

        var act = () => connection.CreateBulkContext<NullableEnabledEntity>("table");

        act.Should().Throw<ArgumentNullException>().WithParameterName("connection");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateBulkContext_NullOrWhitespaceTableName_ThrowsForTableName(string? tableName)
    {
        using var connection = new SqlConnection();

        var act = () => connection.CreateBulkContext<NullableEnabledEntity>(tableName!);

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(tableName));
    }

    [Fact]
    public async Task WriteDataAsync_NullEntities_ThrowsForEntitiesAndLeavesConnectionClosed()
    {
        await using var connection = new SqlConnection();
        var context = connection.CreateBulkContext<NullableEnabledEntity>("table");

        var act = async () => await context.WriteDataAsync((IEnumerable<NullableEnabledEntity>)null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("entities");
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task WriteDataAsync_NullAsyncEntities_ThrowsForEntitiesAndLeavesConnectionClosed()
    {
        await using var connection = new SqlConnection();
        var context = connection.CreateBulkContext<NullableEnabledEntity>("table");

        var act = async () => await context.WriteDataAsync((IAsyncEnumerable<NullableEnabledEntity>)null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("entities");
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task WriteDataAsync_NoMappedProperties_ThrowsAndLeavesConnectionClosed()
    {
        await using var connection = new SqlConnection();
        var context = connection.CreateBulkContext<NullableEnabledEntity>("table");

        var act = async () => await context.WriteDataAsync(Array.Empty<NullableEnabledEntity>());

        await act.Should().ThrowAsync<InvalidOperationException>();
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task WriteDataAsync_AsyncSourceWithNoMappedProperties_ThrowsWithoutEnumerating()
    {
        await using var connection = new SqlConnection();
        var context = connection.CreateBulkContext<NullableEnabledEntity>("table");
        var source = new TrackingAsyncSource<NullableEnabledEntity>(new NullableEnabledEntity());

        var act = async () => await context.WriteDataAsync(source);

        await act.Should().ThrowAsync<InvalidOperationException>();
        source.EnumeratorCount.Should().Be(0);
        connection.State.Should().Be(ConnectionState.Closed);
    }
}
