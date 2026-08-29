using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql.Benchmarks.Probe;

/// <summary>
/// One attempt: build a given number of sources of a given size and copy them all at once by a given
/// mechanism, in a process of its own under a heap ceiling the parent imposed.
/// </summary>
/// <remarks>
/// The exit status is the whole result. Zero means every copy fitted;
/// <see cref="ConfigurationFailure"/> means the attempt could not be set up and says nothing about
/// capacity; anything else means the attempt did not fit, and the parent does not try to tell one
/// cause of death from another because under a hard heap limit they are one finding.
/// <para>
/// The simultaneous copies share this one process, and so share its heap. One service instance is
/// one managed heap, and that is the situation being measured; copies placed in separate processes
/// would each be given the ceiling and would measure several instances instead.
/// </para>
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
    /// <param name="rowCount">How many rows each source holds.</param>
    /// <param name="shareText">Whether the sources may draw their text from the generator's pools.</param>
    /// <param name="copies">How many copies run at once.</param>
    public static async Task<int> RunAsync(
        ProbeShape shape,
        ProbeMechanism mechanism,
        int rowCount,
        bool shareText,
        int copies)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var connections = await PrepareAsync(shape, copies);
        if (connections is null) return ConfigurationFailure;

        try
        {
            // Every source exists before the first copy starts. Their sum is what the ceiling has to
            // hold, and building each one as its copy began would make the peak depend on how the
            // copies happened to interleave.
            var sources = new ProbeSource[copies];

            for (var index = 0; index < copies; index++)
            {
                sources[index] = shape.CreateSource(rowCount, shareText);
            }

            // Each copy gets a thread of its own so that all of them are genuinely in flight at once.
            // Started from this thread instead, they would run one at a time up to their first await:
            // the DataTable route materialises its whole table before it awaits anything, so the
            // first copies would be writing, and freeing their tables, while the last were still
            // building theirs. The peak would then be set by how the copies happened to interleave -
            // by the server's speed against the client's - which is the scheduling artefact this
            // probe exists to keep out of a capacity figure.
            var copied = await Task.WhenAll(Enumerable.Range(0, copies).Select(index =>
                Task.Factory.StartNew(
                    () => sources[index].CopyAsync(connections[index], shape.TableNameFor(index), mechanism),
                    TaskCreationOptions.LongRunning).Unwrap()));

            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"fitted {copies} simultaneous copies, {copied.Sum()} rows"));

            return Fits;
        }
        finally
        {
            foreach (var connection in connections)
            {
                await connection.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Opens one connection per copy and puts an empty destination table in place for each,
    /// reporting a configuration failure rather than throwing so that the parent can tell it apart
    /// from a ceiling.
    /// </summary>
    /// <remarks>
    /// With one connection per copy there are as many ways for the setup to fail as there are
    /// copies, so this guard matters more here than it did when there was one of everything. It is
    /// also the reason the probe stops well below the connection pool's default limit: a pool that
    /// ran out would block and then time out, and a timeout is indistinguishable from a ceiling by
    /// the time the parent sees it.
    /// </remarks>
    /// <param name="shape">The shape whose destinations are being prepared.</param>
    /// <param name="copies">How many connections and tables to prepare.</param>
    /// <returns>The open connections, or <c>null</c> if the attempt cannot be set up.</returns>
    private static async Task<SqlConnection[]?> PrepareAsync(ProbeShape shape, int copies)
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServer.ConnectionStringVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await Console.Error.WriteLineAsync(
                $"{SqlServer.ConnectionStringVariable} is not set. An attempt is started by the probe, not run by hand.");
            return null;
        }

        var opened = new List<SqlConnection>(copies);

        try
        {
            for (var index = 0; index < copies; index++)
            {
                var connection = new SqlConnection(connectionString);
                opened.Add(connection);
                await connection.OpenAsync();

                // Every destination the run could have left behind goes, not only the ones this
                // attempt writes. A search that reached forty-seven copies and then dropped back to
                // one would otherwise leave forty-six populated tables standing for the rest of the
                // run, and a server that then ran out of disk would fail an attempt part-way through
                // its copy. That exits non-zero, which this probe reads as "did not fit", so the
                // recorded boundary would be the container's disk rather than the client's memory.
                if (index == 0) await ExecuteAsync(connection, BuildDropStatement(shape));

                await ExecuteAsync(
                    connection,
                    CopySettings.BuildCreateTableStatement(shape.TableNameFor(index), shape.Columns));
            }

            return [.. opened];
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            foreach (var connection in opened)
            {
                await connection.DisposeAsync();
            }

            await Console.Error.WriteLineAsync("could not prepare the attempt: " + exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Drops every destination table this shape can have, whether or not this attempt uses it.
    /// </summary>
    /// <param name="shape">The shape whose destinations are being cleared.</param>
    private static string BuildDropStatement(ProbeShape shape) =>
        string.Concat(Enumerable
            .Range(0, CapacityProbe.MaximumCopyCount)
            .Select(index => $"DROP TABLE IF EXISTS [{shape.TableNameFor(index)}];"));

    private static async Task ExecuteAsync(SqlConnection connection, string statement)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        await command.ExecuteNonQueryAsync();
    }
}
