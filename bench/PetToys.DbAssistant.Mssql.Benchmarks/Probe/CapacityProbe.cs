using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PetToys.DbAssistant.Mssql.Benchmarks.Probe;

/// <summary>
/// How many rows fit, and how many copies of them fit at once, rather than how fast they copy.
/// </summary>
/// <remarks>
/// <para>
/// The benchmark project measures operations that complete. The failure this library exists to
/// avoid is an operation that does not, so it cannot be measured there and is measured here: under a
/// fixed managed heap ceiling, what is the largest row count each mechanism can copy?
/// </para>
/// <para>
/// The concurrency mode asks the other half of the same question. The workload this package came out
/// of never ran one copy: dozens to hundreds of requests landed on one instance at once, and no
/// single one of them was too large. So with the row count per copy fixed, how many simultaneous
/// copies survive? One instance is one managed heap, so the simultaneous copies of an attempt share
/// one process.
/// </para>
/// <para>
/// Finding a boundary means crossing it, and a process that has crossed it cannot report on itself.
/// Every attempt is therefore a child process whose exit status is the only thing read. The parent
/// doubles until an attempt fails, bisects between the last success and the first failure, and
/// reports the largest figure that succeeded twice - once is a coin flip at the boundary, where GC
/// timing decides.
/// </para>
/// <para>
/// Both mechanisms hold the caller's own collection, so what that collection weighs sets how much of
/// the ceiling is left for anything else, and therefore sets the ratio. The probe measures the same
/// shape twice, once with the generator's pooled text and once with a distinct value per row,
/// because those bracket the range a real caller falls in and a single number would be quoted as
/// though they did not differ.
/// </para>
/// <para>
/// This is not a benchmark and reports no duration, in either mode. It also travels between machines
/// even less well than a duration does: the boundary moves with the ceiling, the GC mode, the
/// pointer size, the runtime and fragmentation. The ratio between two mechanisms in one run is the
/// only quantity worth carrying anywhere.
/// </para>
/// </remarks>
internal static class CapacityProbe
{
    /// <summary>Selects the probe instead of a benchmark run.</summary>
    public const string ProbeSwitch = "--probe";

    /// <summary>Selects one attempt. Set by the probe on its own children, not by hand.</summary>
    public const string AttemptSwitch = "--probe-attempt";

    private const string ConcurrentSwitch = "--concurrent";
    private const string RowsSwitch = "--rows";
    private const string SharedText = "shared";
    private const string DistinctText = "distinct";

    private const int StartRowCount = 25_000;
    private const int MaximumRowCount = 40_000_000;
    private const int RowBisectResolution = 25_000;

    private const int StartCopyCount = 1;
    private const int DefaultRowsPerCopy = 100_000;

    /// <summary>
    /// The largest number of simultaneous copies an attempt will be asked for, chosen below
    /// <c>Microsoft.Data.SqlClient</c>'s default <c>Max Pool Size</c> of 100.
    /// </summary>
    /// <remarks>
    /// An attempt that asked for more connections than the pool allows would block and then time
    /// out, and the probe would record that as a ceiling. It is not one. Raising the pool limit
    /// instead was rejected: a probe that has to tune the connection pool to get its answer is a
    /// probe whose answer includes the connection pool.
    /// <para>
    /// An attempt reads it too, to clear every destination a run could have left behind rather than
    /// only the ones it is about to write.
    /// </para>
    /// </remarks>
    public const int MaximumCopyCount = 64;

    /// <summary>
    /// One copy. The row-count search bisects to twenty-five thousand rows because a finer figure is
    /// noise, but a copy count is a small integer and every step of it is meaningful.
    /// </summary>
    private const int CopyBisectResolution = 1;

    private const int DefaultHeapMegabytes = 512;
    private const int ConfirmationsRequired = 2;

    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMinutes(15);

    /// <summary>Runs one attempt, in the child process the probe started for it.</summary>
    /// <param name="args">The command line, whose first element is <see cref="AttemptSwitch"/>.</param>
    public static async Task<int> RunAttemptAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length < 6)
        {
            await Console.Error.WriteLineAsync(
                $"usage: {AttemptSwitch} <shape> <mechanism> <rows> <{SharedText}|{DistinctText}> <copies>");
            return ProbeAttempt.ConfigurationFailure;
        }

        var shape = ProbeShape.Parse(args[1]);
        if (shape is null)
        {
            await Console.Error.WriteLineAsync($"unknown shape '{args[1]}'.");
            return ProbeAttempt.ConfigurationFailure;
        }

        if (!Enum.TryParse<ProbeMechanism>(args[2], out var mechanism))
        {
            await Console.Error.WriteLineAsync($"unknown mechanism '{args[2]}'.");
            return ProbeAttempt.ConfigurationFailure;
        }

        if (!int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var rowCount))
        {
            await Console.Error.WriteLineAsync($"'{args[3]}' is not a row count.");
            return ProbeAttempt.ConfigurationFailure;
        }

        if (args[4] is not (SharedText or DistinctText))
        {
            await Console.Error.WriteLineAsync($"'{args[4]}' is neither '{SharedText}' nor '{DistinctText}'.");
            return ProbeAttempt.ConfigurationFailure;
        }

        if (!int.TryParse(args[5], NumberStyles.None, CultureInfo.InvariantCulture, out var copies) || copies < 1)
        {
            await Console.Error.WriteLineAsync($"'{args[5]}' is not a number of simultaneous copies.");
            return ProbeAttempt.ConfigurationFailure;
        }

        return await ProbeAttempt.RunAsync(shape, mechanism, rowCount, args[4] == SharedText, copies);
    }

    /// <summary>Runs the probe: both mechanisms over one shape, under one ceiling, twice.</summary>
    /// <param name="args">The command line, which the probe reads its own switches from.</param>
    /// <remarks>
    /// The command line is checked before the server is started. A rejected switch would
    /// otherwise cost a container pull and start before printing one line, which is the same
    /// reason <c>Program</c> answers BenchmarkDotNet's question switches without a server.
    /// </remarks>
    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!TryReadValue(args, "--shape", out var shapeName))
        {
            await Console.Error.WriteLineAsync("--shape was given without a value.");
            return 1;
        }

        var shape = ProbeShape.Parse(shapeName ?? ProbeShape.Narrow.Name);
        if (shape is null)
        {
            await Console.Error.WriteLineAsync("--shape takes 'narrow' or 'wide'.");
            return 1;
        }

        if (!TryReadPositive(args, "--heap", DefaultHeapMegabytes, "a size in megabytes", out var heapMegabytes))
        {
            return 1;
        }

        if (!TryReadPositive(args, RowsSwitch, DefaultRowsPerCopy, "a row count", out var rowsPerCopy))
        {
            return 1;
        }

        var concurrent = args.Contains(ConcurrentSwitch, StringComparer.OrdinalIgnoreCase);

        if (!concurrent && args.Contains(RowsSwitch, StringComparer.OrdinalIgnoreCase))
        {
            // --rows sizes one copy, and the row-count axis is searching for that size rather than
            // being told it. Accepting the switch and then ignoring it would run a measurement the
            // operator did not ask for without telling them, which is the same fault TryReadValue
            // exists to prevent one switch further along.
            await Console.Error.WriteLineAsync(
                $"{RowsSwitch} sets the size of one copy and means nothing without {ConcurrentSwitch}.");
            return 1;
        }

        var serverGc = !args.Contains("--workstation-gc", StringComparer.OrdinalIgnoreCase);
        var heapBytes = (long)heapMegabytes * 1024 * 1024;

        var server = await SqlServer.StartAsync();
        await using var serverScope = server.ConfigureAwait(false);

        // Attempts are children of this process and inherit its environment, which is how they
        // reach the one server. The same handoff BenchmarkDotNet's own child processes use.
        Environment.SetEnvironmentVariable(SqlServer.ConnectionStringVariable, server.ConnectionString);

        WriteHeader(shape, heapMegabytes, serverGc, concurrent, rowsPerCopy, string.Create(
            CultureInfo.InvariantCulture,
            $"Measured against {(server.IsProvisioned ? $"a {SqlServer.Image} container started for this run" : $"the server named by {SqlServer.ConnectionStringVariable}")}."));

        try
        {
            foreach (var shareText in new[] { true, false })
            {
                Console.WriteLine(shareText
                    ? "Source text drawn from the generator's pools:"
                    : "Source text distinct per row:");

                var baseline = await FindCeilingAsync(
                    shape, ProbeMechanism.DataTable, heapBytes, serverGc, shareText, concurrent, rowsPerCopy);
                var mapped = await FindCeilingAsync(
                    shape, ProbeMechanism.MappedBulkContext, heapBytes, serverGc, shareText, concurrent, rowsPerCopy);

                WriteResult(baseline, mapped, concurrent);
            }

            Console.WriteLine();
            WriteClosingNote(concurrent);
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            await Console.Error.WriteLineAsync();
            await Console.Error.WriteLineAsync("The run failed rather than finding a ceiling: " + exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Doubles until an attempt fails, then bisects. Every figure that could be reported has to have
    /// succeeded <see cref="ConfirmationsRequired"/> times; a single failure is enough to exclude
    /// one.
    /// </summary>
    /// <remarks>
    /// One search over both axes. What varies between them is only what the number counts: rows in
    /// one copy, or copies of a fixed number of rows. A second search written for the second axis
    /// would be a second place for the confirmation rule and the lower-bound rule to drift.
    /// </remarks>
    private static async Task<(ProbeMechanism Mechanism, int Value, bool LimitFound)> FindCeilingAsync(
        ProbeShape shape,
        ProbeMechanism mechanism,
        long heapBytes,
        bool serverGc,
        bool shareText,
        bool concurrent,
        int rowsPerCopy)
    {
        var start = concurrent ? StartCopyCount : StartRowCount;
        var maximum = concurrent ? MaximumCopyCount : MaximumRowCount;
        var resolution = concurrent ? CopyBisectResolution : RowBisectResolution;

        var lastFit = 0;
        var firstFailure = 0;
        var value = start;

        while (value <= maximum)
        {
            if (!await FitsAsync(shape, mechanism, value, heapBytes, serverGc, shareText, concurrent, rowsPerCopy))
            {
                firstFailure = value;
                break;
            }

            lastFit = value;
            value *= 2;
        }

        if (firstFailure == 0)
        {
            // Never failed inside the cap. That is a lower bound, and it has to be reported as one.
            return (mechanism, lastFit, false);
        }

        while (firstFailure - lastFit > resolution)
        {
            var middle = lastFit + ((firstFailure - lastFit) / 2);

            if (await FitsAsync(shape, mechanism, middle, heapBytes, serverGc, shareText, concurrent, rowsPerCopy))
            {
                lastFit = middle;
            }
            else
            {
                firstFailure = middle;
            }
        }

        return (mechanism, lastFit, true);
    }

    private static async Task<bool> FitsAsync(
        ProbeShape shape,
        ProbeMechanism mechanism,
        int value,
        long heapBytes,
        bool serverGc,
        bool shareText,
        bool concurrent,
        int rowsPerCopy)
    {
        var rowCount = concurrent ? rowsPerCopy : value;
        var copies = concurrent ? value : 1;

        for (var confirmation = 0; confirmation < ConfirmationsRequired; confirmation++)
        {
            var status = await RunAttemptProcessAsync(
                shape, mechanism, rowCount, heapBytes, serverGc, shareText, copies);

            if (status != ProbeAttempt.Fits)
            {
                WriteAttempt(mechanism, value, concurrent, "did not fit");
                return false;
            }
        }

        WriteAttempt(mechanism, value, concurrent, "fits");
        return true;
    }

    private static async Task<int> RunAttemptProcessAsync(
        ProbeShape shape,
        ProbeMechanism mechanism,
        int rowCount,
        long heapBytes,
        bool serverGc,
        bool shareText,
        int copies)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath
                ?? throw new InvalidOperationException("The probe cannot find its own executable to start an attempt with."),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Launched through the muxer, the assembly has to be named again before its own arguments.
        if (Path.GetFileNameWithoutExtension(startInfo.FileName)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        startInfo.ArgumentList.Add(AttemptSwitch);
        startInfo.ArgumentList.Add(shape.Name);
        startInfo.ArgumentList.Add(mechanism.ToString());
        startInfo.ArgumentList.Add(rowCount.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(shareText ? SharedText : DistinctText);
        startInfo.ArgumentList.Add(copies.ToString(CultureInfo.InvariantCulture));

        // Both are set on every child rather than inherited. A hard limit means a different thing
        // under workstation and server GC, so a result that did not pin the mode is not reproducible.
        startInfo.Environment["DOTNET_GCHeapHardLimit"] = heapBytes.ToString("X", CultureInfo.InvariantCulture);
        startInfo.Environment["DOTNET_gcServer"] = serverGc ? "1" : "0";

        Process process;

        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("An attempt process could not be started.");
        }
        catch (Win32Exception exception)
        {
            // Nothing about this is a ceiling, so it has to fail the run rather than be read
            // as one. Restated as the exception the caller already handles.
            throw new InvalidOperationException(
                "An attempt process could not be started: " + exception.Message,
                exception);
        }

        using (process)
        {
            return await ReadAttemptOutcomeAsync(process, rowCount, copies);
        }
    }

    /// <summary>
    /// Drains the attempt's output, waits for it within the timeout, and turns its exit status
    /// into an outcome.
    /// </summary>
    private static async Task<int> ReadAttemptOutcomeAsync(Process process, int rowCount, int copies)
    {
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(AttemptTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            var attempt = copies == 1
                ? string.Create(CultureInfo.InvariantCulture, $"An attempt at {rowCount:N0} rows")
                : string.Create(CultureInfo.InvariantCulture, $"An attempt at {copies} simultaneous copies of {rowCount:N0} rows");

            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"{attempt} neither finished nor died within {AttemptTimeout.TotalMinutes} minutes. A hang is a defect in the probe and must not be recorded as either outcome."));
        }

        await standardOutput;
        var errorText = await standardError;

        if (process.ExitCode == ProbeAttempt.ConfigurationFailure)
        {
            throw new InvalidOperationException("An attempt could not be set up: " + errorText.Trim());
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Reads the value following a switch, distinguishing a switch that is absent from one that
    /// was given without a value. Silently defaulting the second case would run a shape or a
    /// ceiling the operator did not ask for and would not be told about.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <param name="name">The switch to look for.</param>
    /// <param name="value">The value that followed it, or <c>null</c> if the switch is absent.</param>
    /// <returns><c>false</c> if the switch was present with nothing after it.</returns>
    private static bool TryReadValue(string[] args, string name, out string? value)
    {
        value = null;
        var index = Array.FindIndex(args, argument => argument.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (index < 0) return true;
        if (index + 1 >= args.Length) return false;

        value = args[index + 1];
        return true;
    }

    /// <summary>
    /// Reads a switch whose value has to be a positive number, reporting to standard error and
    /// failing rather than falling back to the default on anything it cannot use.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <param name="name">The switch to look for.</param>
    /// <param name="fallback">The value to use when the switch is absent.</param>
    /// <param name="expectation">What the switch takes, for the error message.</param>
    /// <param name="value">The resolved value.</param>
    /// <returns><c>false</c> if the switch was present and unusable.</returns>
    private static bool TryReadPositive(string[] args, string name, int fallback, string expectation, out int value)
    {
        value = fallback;

        if (!TryReadValue(args, name, out var text))
        {
            Console.Error.WriteLine($"{name} was given without a value.");
            return false;
        }

        if (text is null) return true;

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value <= 0)
        {
            // A ceiling of zero is not a ceiling and a copy of no rows is not a copy. The runtime
            // would either reject such a value or ignore it, and either way every figure below it
            // would describe something else.
            Console.Error.WriteLine($"{name} takes {expectation} greater than zero.");
            return false;
        }

        return true;
    }

    /// <summary>Explains what the reader is looking at, in the terms of the axis they ran.</summary>
    private static void WriteClosingNote(bool concurrent)
    {
        if (concurrent)
        {
            Console.WriteLine("A copy count means nothing without the size of one copy, and it is a small integer, so");
            Console.WriteLine("one either way moves the ratio appreciably. Read it as one significant figure, beside the");
            Console.WriteLine("single-copy result taken at the same ceiling, shape and source weight.");
            return;
        }

        Console.WriteLine("The ratio falls as the caller's own rows get heavier: the collection is a cost both");
        Console.WriteLine("mechanisms pay, and the more of the ceiling it takes, the less the second");
        Console.WriteLine("materialisation can add before neither fits.");
    }

    private static void WriteHeader(
        ProbeShape shape,
        int heapMegabytes,
        bool serverGc,
        bool concurrent,
        int rowsPerCopy,
        string serverDescription)
    {
        Console.WriteLine(concurrent
            ? "Capacity probe: how many simultaneous copies fit, not how fast they copy. No duration is measured."
            : "Capacity probe: how many rows fit, not how fast they copy. No duration is measured.");
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Shape: {shape.Name} ({shape.Columns.Count} columns). Heap ceiling: {heapMegabytes} MB. GC: {(serverGc ? "server" : "workstation")}. Pointer size: {IntPtr.Size * 8}-bit."));

        if (concurrent)
        {
            // Named in the header because a copy count without the size of one copy invites the
            // reader to supply their own and reach a wrong conclusion.
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Rows per copy: {rowsPerCopy:N0}. Attempts stop at {MaximumCopyCount} simultaneous copies, below the connection pool's default limit."));
        }

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Runtime: {RuntimeInformation.FrameworkDescription}. {serverDescription}"));
        Console.WriteLine();
    }

    private static void WriteAttempt(ProbeMechanism mechanism, int value, bool concurrent, string outcome) =>
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  {mechanism,-18} {value,12:N0} {Unit(value, concurrent)}  {outcome}"));

    /// <summary>Names what an attempt's figure counts, padded so the outcomes stay in one column.</summary>
    private static string Unit(int value, bool concurrent) => concurrent
        ? (value == 1 ? "copy  " : "copies")
        : "rows  ";

    private static void WriteResult(
        (ProbeMechanism Mechanism, int Value, bool LimitFound) baseline,
        (ProbeMechanism Mechanism, int Value, bool LimitFound) mapped,
        bool concurrent)
    {
        Console.WriteLine();
        Console.WriteLine(concurrent
            ? "  Largest number of simultaneous copies that fitted, confirmed twice:"
            : "  Largest row count that fitted, confirmed twice:");
        WriteCeiling(baseline, concurrent);
        WriteCeiling(mapped, concurrent);

        Console.WriteLine(baseline.LimitFound && mapped.LimitFound && baseline.Value > 0
            ? string.Create(CultureInfo.InvariantCulture, $"    Ratio: {(double)mapped.Value / baseline.Value:F2}x")
            : "    No ratio: at least one mechanism produced no figure to divide.");

        Console.WriteLine();
    }

    private static void WriteCeiling((ProbeMechanism Mechanism, int Value, bool LimitFound) ceiling, bool concurrent) =>
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"    {ceiling.Mechanism,-18} {Describe(ceiling, concurrent)}"));

    private static string Describe((ProbeMechanism Mechanism, int Value, bool LimitFound) ceiling, bool concurrent) =>
        ceiling switch
        {
            // Not "0". A count of zero reads as a measurement, and this is the absence of one: the
            // configured row count does not fit even once, so there is no concurrency to speak of.
            { Value: 0 } when concurrent => "none  (a single copy of this size does not fit)",
            { Value: 0 } => "none  (the ceiling is below the smallest attempt)",
            { LimitFound: false } => string.Create(
                CultureInfo.InvariantCulture,
                $"{ceiling.Value,12:N0}  (a lower bound; the limit was not found)"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{ceiling.Value,12:N0}"),
        };
}
