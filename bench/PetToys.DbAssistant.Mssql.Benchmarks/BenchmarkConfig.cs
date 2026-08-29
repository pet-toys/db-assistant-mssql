using System;
using System.IO;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The one configuration every benchmark in this assembly runs under.
/// </summary>
/// <remarks>
/// <para>
/// It is built on top of <see cref="DefaultConfig"/> rather than from nothing, so the default
/// loggers, columns, analysers, validators and exporters stay in place - including the
/// GitHub-flavoured markdown export the recorded baseline is a copy of - and it is applied through
/// <c>BenchmarkSwitcher</c> rather than through an attribute on every class, so the command line
/// keeps its say over the job and the runtimes.
/// </para>
/// <para>
/// The job is the part that is not default. One benchmark here is one bulk copy of ten or a hundred
/// thousand rows, which is several orders of magnitude past the point where BenchmarkDotNet's pilot
/// stage and its batching of invocations per iteration earn their keep, and the destination table
/// has to be emptied between copies or every iteration after the first measures a larger table than
/// the one before it. <see cref="RunStrategy.Monitoring"/> with an invocation count and an unroll
/// factor of one is what makes <c>[IterationSetup]</c> meaningful: one setup, one timed copy, one
/// sample. The cost is that a run collects tens of samples rather than thousands, so the standard
/// deviation in the report is worth a look before quoting a mean.
/// </para>
/// <para>
/// The warmup is five rather than the default two. That is not a preference: at the default, the
/// sibling package recorded a ratio of 1.50 that no later run reproduced anywhere. A freshly started
/// server spends its first copies filling caches and settling, and measuring those is how a mapping
/// layer gets blamed for half again its actual cost.
/// </para>
/// <para>
/// The memory diagnoser is the other addition, and it is read here with more care than usual. Every
/// arm boxes - <c>IDataRecord.GetValue(int)</c> returns <see cref="object"/>, so a value-type column
/// boxes once per row no matter who wrote the reader - which is why the allocation figures are close
/// between the reader arms. The <c>DataTable</c> arm's disadvantage is that it retains rather than
/// that it allocates, and retention shows in the collection counts rather than in
/// <c>Allocated</c>.
/// </para>
/// <para>
/// There are three jobs, one per supported runtime, and they are identical in everything but the
/// runtime. That is deliberate and it replaces passing <c>--runtimes</c> on the command line, which
/// does not do what it looks like it does: it does not clone the configured job across the runtimes
/// named, it <em>adds</em> a default-configured job for each of them beside it. A run made that way
/// measures three runtimes with BenchmarkDotNet's defaults and only one with the settings this class
/// exists to impose, and it reports both in a single table as though they were one measurement.
/// </para>
/// <para>
/// Declaring the jobs here also fixes what that mistake did to the <c>Ratio</c> column. With the
/// baseline on a method and several jobs in the table, the report picked one row as the reference
/// for the whole group, so a runtime's baseline arm was being divided by another runtime's rather
/// than by its own, and the column silently stopped comparing arms and started comparing runtimes.
/// <see cref="BenchmarkLogicalGroupRule.ByJob"/> together with
/// <see cref="BenchmarkLogicalGroupRule.ByParams"/> states the grouping outright: one baseline per
/// runtime per row count, which is the only reading of a ratio this project has any use for.
/// </para>
/// <para>
/// The artifacts path is pinned to the directory the benchmark assembly was built into, rather than
/// left at its default of the current working directory. The default puts a run's output wherever
/// the caller happened to be standing, so running from the repository root and running from the
/// project folder produce two artifact directories that neither knows about the other. Keying it to
/// the assembly also separates the target frameworks, which is right: a net8.0 run and a net10.0 run
/// are not each other's results.
/// </para>
/// </remarks>
public static class BenchmarkConfig
{
    /// <summary>Builds the configuration.</summary>
    /// <returns>The configuration to hand to the switcher.</returns>
    public static IConfig Create() =>
        ManualConfig.Create(DefaultConfig.Instance)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddJob(CopyJob(CoreRuntime.Core80))
            .AddJob(CopyJob(CoreRuntime.Core90))
            .AddJob(CopyJob(CoreRuntime.Core10_0))
            .AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByJob, BenchmarkLogicalGroupRule.ByParams)
            .WithArtifactsPath(Path.Combine(AppContext.BaseDirectory, "BenchmarkDotNet.Artifacts"));

    /// <summary>Builds the one job shape, pinned to a runtime.</summary>
    /// <param name="runtime">The runtime to measure on.</param>
    /// <returns>The job to add to the configuration.</returns>
    private static Job CopyJob(Runtime runtime) =>
        Job.Default
            .WithRuntime(runtime)
            .WithStrategy(RunStrategy.Monitoring)
            .WithInvocationCount(1)
            .WithUnrollFactor(1)
            .WithWarmupCount(5)
            .WithIterationCount(15)
            .WithId($"Copy {runtime.Name}");
}
