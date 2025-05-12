using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace PetToys.DbAssistant.Mssql.Test.Bulk;

public sealed class MsSqlFixture(IMessageSink messageSink)
    : DbContainerFixture<MsSqlBuilder, MsSqlContainer>(messageSink)
{
    public override SqlClientFactory DbProviderFactory => SqlClientFactory.Instance;
}
