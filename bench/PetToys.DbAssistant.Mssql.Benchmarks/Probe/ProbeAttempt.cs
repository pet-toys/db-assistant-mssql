using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql.Benchmarks.Probe;

/// <summary>
/// One attempt: build a source of a given size and copy it by a given mechanism, in a process of its
/// own under a heap ceiling the parent imposed.
/// </summary>
/// <remarks>
/// The exit status is the whole result. Zero means the rows fitted;
/// <see cref="ConfigurationFailure"/> means the attempt could not be set up and says nothing about
/// capacity; anything else means the attempt did not fit, and the parent does not try to tell one
/// cause of death from another because under a hard heap limit they are one finding.
/// <para>
/// Nothing after the setup is guarded. A handler that runs once the heap is exhausted is a handler
/// that may not run, and one that succeeded in running would turn a boundary into a report from a
/// process that has no business writing one.
/// </para>
/// </remarks>
internal static class ProbeAttempt
{
    /// <summary>The exit status of an attempt whose rows fitted.</summary>
    public const int Fits = 0;

    /// <summary>
    /// The exit status of an attempt that could not be set up: no server, no table, bad arguments.
    /// Distinct from every other non-zero status because a boundary found by an unreachable database
    /// is worse than no boundary at all.
    /// </summary>
    public const int ConfigurationFailure = 2;

    /// <summary>Runs one attempt and returns its exit status.</summary>
    /// <param name="shape">The row shape to build and copy.</param>
    /// <param name="mechanism">Which of the two routes to take.</param>
    /// <param name="rowCount">How many rows to build and copy.</param>
    /// <param name="shareText">Whether the source may draw its text from the generator's pools.</param>
    public static async Task<int> RunAsync(
        ProbeShape shape,
        ProbeMechanism mechanism,
        int rowCount,
        bool shareText)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var connection = await PrepareAsync(shape);
        if (connection is null) return ConfigurationFailure;

        await using (connection.ConfigureAwait(false))
        {
            var copied = await shape.CopyAsync(connection, mechanism, rowCount, shareText);

            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"fitted {copied} rows"));

            return Fits;
        }
    }

    /// <summary>
    /// Opens the connection and puts an empty destination table in place, reporting a configuration
    /// failure rather than throwing so that the parent can tell it apart from a ceiling.
    /// </summary>
    /// <param name="shape">The shape whose destination is being prepared.</param>
    /// <returns>The open connection, or <c>null</c> if the attempt cannot be set up.</returns>
    private static async Task<SqlConnection?> PrepareAsync(ProbeShape shape)
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServer.ConnectionStringVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await Console.Error.WriteLineAsync(
                $"{SqlServer.ConnectionStringVariable} is not set. An attempt is started by the probe, not run by hand.");
            return null;
        }

        SqlConnection? connection = null;

        try
        {
            connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"DROP TABLE IF EXISTS [{shape.TableName}];");
            await ExecuteAsync(connection, CopySettings.BuildCreateTableStatement(shape.TableName, shape.Columns));
            return connection;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (connection is not null) await connection.DisposeAsync();
            await Console.Error.WriteLineAsync("could not prepare the attempt: " + exception.Message);
            return null;
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string statement)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        await command.ExecuteNonQueryAsync();
    }
}
