using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PetToys.DbAssistant.Mssql.Extensions;

namespace PetToys.DbAssistant.Mssql.Test;

public abstract class DatabaseTestBase
{
    private readonly string _connectionString;

    protected DatabaseTestBase()
    {
        AssertionConfiguration.Current.Equivalency.Modify(options => options.WithStrictOrdering());

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DatabaseTestBase>()
            .Build();
        var builder = new SqlConnectionStringBuilder(configuration.GetConnectionString("TestSqlConnection"));
        _connectionString = builder.ConnectionString;
    }

    protected async Task<SqlConnection> ReCreateTableAsync(string tableName)
    {
        var query = ($"""
                      DROP TABLE IF EXISTS {tableName.QuoteName()};
                      CREATE TABLE {tableName.QuoteName()} (
                        [Int0] int NOT NULL
                       ,[Int1] int
                       ,[Date0] datetime NOT NULL
                       ,[Date1] datetime
                       ,[Str0] varchar(500) NOT NULL
                       ,[Str1] varchar(500)
                       ,[Arr0] varbinary(500) NOT NULL
                       ,[Arr1] varbinary(500)
                      );
                      """);

        var connection = GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    protected SqlConnection GetConnection() => new(_connectionString);
}