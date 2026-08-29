using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The lifecycle every benchmark in this assembly shares: the server, the connection, the
/// destination table and the rows to copy into it.
/// </summary>
/// <typeparam name="TRow">The row type being copied.</typeparam>
/// <remarks>
/// <para>
/// The setup runs once per parameter combination and the truncate once per iteration, so a timed
/// region holds one copy into an empty table and nothing else - no connection to open, no rows to
/// build, no table to create.
/// </para>
/// <para>
/// Every destination is a heap with no index and no constraint, in a database whose recovery model
/// the runner reports, and every arm copies with <see cref="SqlBulkCopyOptions.TableLock"/>.
/// Together those are what SQL Server needs to take the minimally-logged path, which is the nearest
/// thing it has to the sibling package's unlogged tables. It falls on every arm equally, so ratios
/// are unaffected; what it costs is that a duration read off this benchmark is a floor rather than
/// what a copy into a real, indexed, fully-logged table costs.
/// </para>
/// <para>
/// <see cref="CreateCopier"/> and <see cref="ConfigureLikeTheOtherArms"/> exist so that no arm can
/// drift from the others. Two arms are only comparable if the <see cref="SqlBulkCopy"/> underneath
/// them is configured identically, and a difference in any one setting silently invalidates every
/// ratio in the report without appearing anywhere in it.
/// </para>
/// </remarks>
public abstract class BulkCopyHarness<TRow>
    where TRow : class
{
    private SqlServer _server = null!;

    /// <summary>
    /// How many rows one copy carries. The values live on each concrete class rather than here: the
    /// off-row streaming class carries a large value per row and cannot afford the counts the
    /// row-shape classes use.
    /// </summary>
    public virtual int RowCount { get; set; }

    /// <summary>The open connection every arm copies through.</summary>
    protected SqlConnection Connection { get; private set; } = null!;

    /// <summary>The rows to copy, built before anything is timed.</summary>
    protected IReadOnlyList<TRow> Rows { get; private set; } = null!;

    /// <summary>The unquoted name of the destination table.</summary>
    protected abstract string TableName { get; }

    /// <summary>The destination columns, in the order every arm writes them.</summary>
    protected abstract IReadOnlyList<ColumnSpec> Columns { get; }

    /// <summary>Builds the rows for one parameter combination.</summary>
    /// <param name="count">How many rows to build.</param>
    protected abstract IReadOnlyList<TRow> GenerateRows(int count);

    [GlobalSetup]
    public async Task GlobalSetupAsync()
    {
        // A RowCount of zero means the [Params] on the concrete class was not picked up, and every
        // arm would then copy nothing, quickly, and report it as a result. Fail instead.
        if (RowCount <= 0)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} was set up with RowCount {RowCount}. A concrete benchmark class has to declare its own [Params] for it.");
        }

        _server = await SqlServer.StartAsync();
        Connection = new SqlConnection(_server.ConnectionString);
        await Connection.OpenAsync();

        await ExecuteAsync($"DROP TABLE IF EXISTS [{TableName}];");
        await ExecuteAsync(CopySettings.BuildCreateTableStatement(TableName, Columns));

        Rows = GenerateRows(RowCount);
        OnRowsBuilt();
    }

    /// <summary>
    /// Empties the table before every timed copy. <c>TRUNCATE</c> rather than <c>DROP</c>/
    /// <c>CREATE</c>: the table definition is not what a copy benchmark should be measuring, and a
    /// table that grew across iterations would make each one slower than the last for a reason that
    /// has nothing to do with this library.
    /// </summary>
    [IterationSetup]
    public void TruncateTable()
    {
        using var command = Connection.CreateCommand();
        command.CommandText = $"TRUNCATE TABLE [{TableName}];";
        command.ExecuteNonQuery();
    }

    [GlobalCleanup]
    public async Task GlobalCleanupAsync()
    {
        OnCleanup();
        await Connection.DisposeAsync();
        await _server.DisposeAsync();
    }

    /// <summary>
    /// Called once the rows exist and before anything is timed, for whatever else an arm needs
    /// prepared out of the measurement.
    /// </summary>
    protected virtual void OnRowsBuilt()
    {
    }

    /// <summary>Called before the connection and the server are disposed.</summary>
    protected virtual void OnCleanup()
    {
    }

    /// <summary>
    /// Configures the copier every raw arm shares, identically to what the library configures for a
    /// mapped one.
    /// </summary>
    protected SqlBulkCopy CreateCopier() =>
        CopySettings.CreateCopier(Connection, TableName, Columns);

    /// <summary>
    /// Puts a mapped arm on exactly the configuration <see cref="CreateCopier"/> builds for the raw
    /// ones.
    /// </summary>
    /// <remarks>
    /// The two defaults agree today, so this sets what both sides would have arrived at anyway.
    /// It is written out rather than left implicit because the arms have already differed in this
    /// one flag once: a difference of a single setting moves every number in the report, appears
    /// nowhere in it, and gives no sign that the comparison stopped being one.
    /// </remarks>
    /// <param name="options">The options the library is about to copy with.</param>
    protected static void ConfigureLikeTheOtherArms(SqlBulkOptions options) =>
        CopySettings.Apply(options);

    /// <summary>
    /// The shared configuration with the one flag the streaming classes are about turned on, which
    /// is the departure from the default a caller makes deliberately when their rows carry values
    /// large enough to be stored off-row.
    /// </summary>
    /// <remarks>
    /// It lives here rather than on either streaming class because both need it and because a
    /// second copy of it is a second chance for the two comparisons to stop being the same
    /// comparison.
    /// </remarks>
    /// <param name="options">The options the library is about to copy with.</param>
    protected static void ConfigureWithStreamingOn(SqlBulkOptions options)
    {
        ConfigureLikeTheOtherArms(options);
        options.EnableStreaming = true;
    }

    /// <summary>Copies through a reader with the shared configuration.</summary>
    /// <param name="reader">The reader to drain.</param>
    protected async Task<long> CopyReaderAsync(DbDataReader reader)
    {
        using var copier = CreateCopier();
        await copier.WriteToServerAsync(reader);
        return copier.RowsCopied64;
    }

    private async Task ExecuteAsync(string statement)
    {
        await using var command = Connection.CreateCommand();
        command.CommandText = statement;
        await command.ExecuteNonQueryAsync();
    }
}
