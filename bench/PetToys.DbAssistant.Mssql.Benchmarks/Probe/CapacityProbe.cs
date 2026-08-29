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
/// How many rows fit, rather than how fast they copy.
/// </summary>
/// <remarks>
/// <para>
/// The benchmark project measures operations that complete. The failure this library exists to
/// avoid is an operation that does not, so it cannot be measured there and is measured here: under a
/// fixed managed heap ceiling, what is the largest row count each mechanism can copy?
/// </para>
/// <para>
/// Finding a boundary means crossing it, and a process that has crossed it cannot report on itself.
/// Every attempt is therefore a child process whose exit status is the only thing read. The parent
/// doubles the row count until an attempt fails, bisects between the last success and the first
/// failure, and reports the largest row count that succeeded twice - once is a coin flip at the
/// boundary, where GC timing decides.
/// </para>
/// <para>
/// Both mechanisms hold the caller's own collection, so what that collection weighs sets how much of
/// the ceiling is left for anything else, and therefore sets the ratio. The probe measures the same
/// shape twice, once with the generator's pooled text and once with a distinct value per row,
/// because those bracket the range a real caller falls in and a single number would be quoted as
/// though they did not differ.
/// </para>
/// <para>
/// This is not a benchmark and reports no duration. It also travels between machines even less well
/// than a duration does: the boundary moves with the ceiling, the GC mode, the pointer size, the
/// runtime and fragmentation. The ratio between two mechanisms in one run is the only quantity worth
/// carrying anywhere.
/// </para>
/// </remarks>
internal static class CapacityProbe
{
    /// <summary>Selects the probe instead of a benchmark run.</summary>
    public const string ProbeSwitch = "--probe";

    /// <summary>Selects one attempt. Set by the probe on its own children, not by hand.</summary>
    public const string AttemptSwitch = "--probe-attempt";

    private const string SharedText = "shared";
    private const string DistinctText = "distinct";

    private const int StartRowCount = 25_000;
    private const int MaximumRowCount = 40_000_000;
    private const int BisectResolution = 25_000;
    private const int DefaultHeapMegabytes = 512;
    private const int ConfirmationsRequired = 2;

    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMinutes(15);

    /// <summary>Runs one attempt, in the child process the probe started for it.</summary>
    /// <param name="args">The command line, whose first element is <see cref="AttemptSwitch"/>.</param>
    public static async Task<int> RunAttemptAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length < 5)
        {
            await Console.Error.WriteLineAsync(
                $"usage: {AttemptSwitch} <shape> <mechanism> <rows> <{SharedText}|{DistinctText}>");
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

        return await ProbeAttempt.RunAsync(shape, mechanism, rowCount, args[4] == SharedText);
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

        if (!TryReadValue(args, "--heap", out var heapText))
        {
            await Console.Error.WriteLineAsync("--heap was given without a value.");
            return 1;
        }

        var heapMegabytes = DefaultHeapMegabytes;

        if (heapText is not null
            && !int.TryParse(heapText, NumberStyles.None, CultureInfo.InvariantCulture, out heapMegabytes))
        {
            await Console.Error.WriteLineAsync("--heap takes a size in megabytes.");
            return 1;
        }

        if (heapMegabytes <= 0)
        {
            // A ceiling of zero is not a ceiling. The runtime would either reject it or ignore
            // it, and either way every row count below would describe something else.
            await Console.Error.WriteLineAsync("--heap takes a positive size in megabytes.");
            return 1;
        }

        var serverGc = !args.Contains("--workstation-gc", StringComparer.OrdinalIgnoreCase);
        var heapBytes = (long)heapMegabytes * 1024 * 1024;

        var server = await SqlServer.StartAsync();
        await using var serverScope = server.ConfigureAwait(false);

        // Attempts are children of this process and inherit its environment, which is how they
        // reach the one server. The same handoff BenchmarkDotNet's own child processes use.
        Environment.SetEnvironmentVariable(SqlServer.ConnectionStringVariable, server.ConnectionString);

        WriteHeader(shape, heapMegabytes, serverGc, string.Create(
            CultureInfo.InvariantCulture,
            $"Measured against {(server.IsProvisioned ? $"a {SqlServer.Image} container started for this run" : $"the server named by {SqlServer.ConnectionStringVariable}")}."));

        try
        {
            foreach (var shareText in new[] { true, false })
            {
                Console.WriteLine(shareText
                    ? "Source text drawn from the generator's pools:"
                    : "Source text distinct per row:");

                var baseline = await FindCeilingAsync(shape, ProbeMechanism.DataTable, heapBytes, serverGc, shareText);
                var mapped = await FindCeilingAsync(shape, ProbeMechanism.MappedBulkContext, heapBytes, serverGc, shareText);

                WriteResult(baseline, mapped);
            }

            Console.WriteLine();
            Console.WriteLine("The ratio falls as the caller's own rows get heavier: the collection is a cost both");
            Console.WriteLine("mechanisms pay, and the more of the ceiling it takes, the less the second");
            Console.WriteLine("materialisation can add before neither fits.");
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
    /// Doubles until an attempt fails, then bisects. Every row count that could be reported has to
    /// have succeeded <see cref="ConfirmationsRequired"/> times; a single failure is enough to
    /// exclude one.
    /// </summary>
    private static async Task<(ProbeMechanism Mechanism, int Rows, bool LimitFound)> FindCeilingAsync(
        ProbeShape shape,
        ProbeMechanism mechanism,
        long heapBytes,
        bool serverGc,
        bool shareText)
    {
        var lastFit = 0;
        var firstFailure = 0;
        var rowCount = StartRowCount;

        while (rowCount <= MaximumRowCount)
        {
            if (!await FitsAsync(shape, mechanism, rowCount, heapBytes, serverGc, shareText))
            {
                firstFailure = rowCount;
                break;
            }

            lastFit = rowCount;
            rowCount *= 2;
        }

        if (firstFailure == 0)
        {
            // Never failed inside the cap. That is a lower bound, and it has to be reported as one.
            return (mechanism, lastFit, false);
        }

        while (firstFailure - lastFit > BisectResolution)
        {
            var middle = lastFit + ((firstFailure - lastFit) / 2);

            if (await FitsAsync(shape, mechanism, middle, heapBytes, serverGc, shareText))
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
        int rowCount,
        long heapBytes,
        bool serverGc,
        bool shareText)
    {
        for (var confirmation = 0; confirmation < ConfirmationsRequired; confirmation++)
        {
            var status = await RunAttemptProcessAsync(shape, mechanism, rowCount, heapBytes, serverGc, shareText);

            if (status != ProbeAttempt.Fits)
            {
                WriteAttempt(mechanism, rowCount, "did not fit");
                return false;
            }
        }

        WriteAttempt(mechanism, rowCount, "fits");
        return true;
    }

    private static async Task<int> RunAttemptProcessAsync(
        ProbeShape shape,
        ProbeMechanism mechanism,
        int rowCount,
        long heapBytes,
        bool serverGc,
        bool shareText)
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
            return await ReadAttemptOutcomeAsync(process, rowCount);
        }
    }

    /// <summary>
    /// Drains the attempt's output, waits for it within the timeout, and turns its exit status
    /// into an outcome.
    /// </summary>
    private static async Task<int> ReadAttemptOutcomeAsync(Process process, int rowCount)
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
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"An attempt at {rowCount} rows neither finished nor died within {AttemptTimeout.TotalMinutes} minutes. A hang is a defect in the probe and must not be recorded as either outcome."));
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

    private static void WriteHeader(ProbeShape shape, int heapMegabytes, bool serverGc, string serverDescription)
    {
        Console.WriteLine("Capacity probe: how many rows fit, not how fast they copy. No duration is measured.");
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Shape: {shape.Name} ({shape.Columns.Count} columns). Heap ceiling: {heapMegabytes} MB. GC: {(serverGc ? "server" : "workstation")}. Pointer size: {IntPtr.Size * 8}-bit."));
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Runtime: {RuntimeInformation.FrameworkDescription}. {serverDescription}"));
        Console.WriteLine();
    }

    private static void WriteAttempt(ProbeMechanism mechanism, int rowCount, string outcome) =>
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  {mechanism,-18} {rowCount,12:N0} rows  {outcome}"));

    private static void WriteResult(
        (ProbeMechanism Mechanism, int Rows, bool LimitFound) baseline,
        (ProbeMechanism Mechanism, int Rows, bool LimitFound) mapped)
    {
        Console.WriteLine();
        Console.WriteLine("  Largest row count that fitted, confirmed twice:");
        WriteCeiling(baseline);
        WriteCeiling(mapped);

        Console.WriteLine(baseline.LimitFound && mapped.LimitFound && baseline.Rows > 0
            ? string.Create(CultureInfo.InvariantCulture, $"    Ratio: {(double)mapped.Rows / baseline.Rows:F2}x")
            : "    No ratio: at least one mechanism's limit was not found inside the attempt cap.");

        Console.WriteLine();
    }

    private static void WriteCeiling((ProbeMechanism Mechanism, int Rows, bool LimitFound) ceiling) =>
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"    {ceiling.Mechanism,-18} {Describe(ceiling)}"));

    private static string Describe((ProbeMechanism Mechanism, int Rows, bool LimitFound) ceiling) => ceiling switch
    {
        { Rows: 0 } => "none  (the ceiling is below the smallest attempt)",
        { LimitFound: false } => string.Create(
            CultureInfo.InvariantCulture,
            $"{ceiling.Rows,12:N0}  (a lower bound; the limit was not found)"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{ceiling.Rows,12:N0}"),
    };
}
