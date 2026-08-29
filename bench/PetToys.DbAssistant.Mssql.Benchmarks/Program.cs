using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using PetToys.DbAssistant.Mssql.Benchmarks.Probe;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>The entry point of the benchmark runner.</summary>
public static class Program
{
    /// <summary>
    /// Switches BenchmarkDotNet answers rather than runs: none of them needs a server, and starting
    /// a container to print a help screen is a minute nobody asked for.
    /// </summary>
    private static readonly string[] QuestionSwitches = ["--help", "-h", "-?", "--list", "--version", "--info"];

    /// <summary>Runs the benchmarks, or the capacity probe, that the command line selects.</summary>
    /// <param name="args">
    /// BenchmarkDotNet's own switches - <c>--filter</c>, <c>--job</c> and the rest - passed through
    /// untouched, which is why the configuration lives in one place instead of on every benchmark
    /// class. <c>--probe</c> selects the capacity probe instead, and <c>--probe-attempt</c> is how
    /// the probe starts its own children rather than something to type.
    /// </param>
    /// <returns>Zero when the run finished, non-zero when it failed rather than producing a result.</returns>
    /// <remarks>
    /// The server is started here, once, and not in <c>[GlobalSetup]</c>. BenchmarkDotNet runs every
    /// benchmark case - each method at each parameter value - in a process of its own, so a
    /// container started from the setup is a container per case: the arms of every ratio would be
    /// measured against different servers, which is exactly the difference a ratio is supposed to
    /// cancel out. Child processes inherit this process's environment, so publishing the connection
    /// string into <see cref="SqlServer.ConnectionStringVariable"/> is what makes all of them share
    /// the one server - and an operator who set that variable themselves keeps their own. The probe
    /// starts children for the same reason and reaches them the same way.
    /// </remarks>
    public static async Task<int> Main(string[] args)
    {
        // An attempt is a child of a probe that has already started the server and published the
        // connection string. It must not start one of its own, and it must not be reached by any of
        // the branches below.
        if (args.Length > 0 && args[0].Equals(CapacityProbe.AttemptSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return await CapacityProbe.RunAttemptAsync(args);
        }

        if (args.Any(argument => QuestionSwitches.Contains(argument, StringComparer.OrdinalIgnoreCase)))
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchmarkConfig.Create());
            return 0;
        }

        // The probe owns its own server lifecycle, so that it can reject a bad switch before a
        // container is started rather than after.
        if (args.Any(argument => argument.Equals(CapacityProbe.ProbeSwitch, StringComparison.OrdinalIgnoreCase)))
        {
            return await CapacityProbe.RunAsync(args);
        }

        var server = await SqlServer.StartAsync();
        await using (server.ConfigureAwait(false))
        {
            Environment.SetEnvironmentVariable(SqlServer.ConnectionStringVariable, server.ConnectionString);

            var description = server.IsProvisioned
                ? $"a {SqlServer.Image} container started for this run"
                : $"the server named by {SqlServer.ConnectionStringVariable}";

            var recovery = await server.ReadRecoveryModelAsync();

            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Measuring against {description}."));
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Recovery model: {recovery}. Minimal logging needs SIMPLE or BULK_LOGGED; under FULL the durations below include the transaction log."));

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchmarkConfig.Create());
            return 0;
        }
    }
}
