using System;
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
    public void CreateBulkContext_NullConnection_Throws()
    {
        SqlConnection connection = null!;

        var act = () => connection.CreateBulkContext<NullableEnabledEntity>("table");

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateBulkContext_NullOrWhitespaceTableName_Throws(string? tableName)
    {
        using var connection = new SqlConnection();

        var act = () => connection.CreateBulkContext<NullableEnabledEntity>(tableName!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task WriteDataAsync_NullEntities_Throws()
    {
        using var connection = new SqlConnection();
        var context = connection.CreateBulkContext<NullableEnabledEntity>("table");

        var act = async () => await context.WriteDataAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
