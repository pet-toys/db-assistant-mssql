using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The server a run measures against.
/// </summary>
/// <remarks>
/// <para>
/// By default the run provisions its own: a container from a pinned image, started once for the
/// whole run by <see cref="Program"/> and published to the benchmark processes through
/// <see cref="ConnectionStringVariable"/>. It is deliberately not started from
/// <c>[GlobalSetup]</c>: BenchmarkDotNet runs every benchmark case in a process of its own, so that
/// would be one container per case, and the arms of a ratio would be measured against different
/// servers. The image is heavy enough that it would also cost minutes of pure container startup.
/// </para>
/// <para>
/// A container over Docker Desktop is a fair place to compare two versions of this library against
/// each other and a poor stand-in for the server anybody actually copies into. Setting
/// <see cref="ConnectionStringVariable"/> points the run at a server of the operator's own, which is
/// the only way to get a duration that means anything outside this repository.
/// </para>
/// </remarks>
public sealed class SqlServer : IAsyncDisposable
{
    /// <summary>Names a server to measure against instead of starting a container.</summary>
    public const string ConnectionStringVariable = "MSSQL_BENCHMARK_CONNECTION_STRING";

    /// <summary>
    /// The image a provisioned server runs. Pinned rather than left to the Testcontainers default:
    /// the recorded baseline names a server version, and a run that silently moved to another one
    /// would not be comparable against it. This is the tag the test suite's fixture resolves to, so
    /// both measure against the same build.
    /// </summary>
    public const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private readonly MsSqlContainer? _container;

    private SqlServer(MsSqlContainer? container, string connectionString)
    {
        _container = container;
        ConnectionString = connectionString;
    }

    /// <summary>The connection string of the server this run measures against.</summary>
    public string ConnectionString { get; }

    /// <summary>Whether the server was provisioned by this run rather than named by the operator.</summary>
    public bool IsProvisioned => _container is not null;

    /// <summary>Starts, or connects to, the server the run measures against.</summary>
    public static async Task<SqlServer> StartAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new SqlServer(container: null, configured);
        }

        var container = new MsSqlBuilder(Image).Build();
        await container.StartAsync();

        return new SqlServer(container, container.GetConnectionString());
    }

    /// <summary>
    /// Reads the recovery model of the database the connection string names.
    /// </summary>
    /// <remarks>
    /// Minimal logging needs SIMPLE (or BULK_LOGGED) recovery as well as a heap destination and
    /// <c>TABLOCK</c> on the copy. A provisioned container's database is SIMPLE; a server named by
    /// the operator may not be, and a run against a FULL database measures the transaction log as
    /// much as it measures anything else. Reporting it rather than changing it is deliberate: the
    /// run does not own somebody else's database.
    /// </remarks>
    public async Task<string> ReadRecoveryModelAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Recovery') AS nvarchar(32));";
        return await command.ExecuteScalarAsync() as string ?? "UNKNOWN";
    }

    /// <summary>Stops the container this run started, if it started one.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
