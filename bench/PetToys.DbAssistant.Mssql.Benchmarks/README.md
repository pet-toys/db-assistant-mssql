# Benchmarks

What one bulk copy costs through this library, measured against the three other
ways a caller could get the same rows into SQL Server. The package's Description
claims "no intermediate DataTable and no hand-written IDataReader"; this project
is where those two claims meet a number.

Nothing here is a gate. No build, pull request or release fails because of
anything in this project - see [Why this is not in CI](#why-this-is-not-in-ci).

## Running it

From the repository root. The project multi-targets, so a framework has to be
named:

```bash
dotnet run -c Release -f net10.0 --project bench/PetToys.DbAssistant.Mssql.Benchmarks -- --filter "*"
```

The run needs a Docker engine that can run Linux containers, the same thing the
integration tests need. It starts one `mcr.microsoft.com/mssql/server` container
for the whole run and stops it again at the end. The image is around 1.5 GB and
wants roughly 2 GB of memory, so a first run spends a while pulling before it
measures anything.

The container is started by the runner and handed to the benchmark processes
through `MSSQL_BENCHMARK_CONNECTION_STRING`, not started from `[GlobalSetup]`.
BenchmarkDotNet gives every benchmark case - each method at each row count - a
process of its own, so a container started from the setup is a container per
case, and the baseline and the measured arm of a ratio end up on two different
servers. That is the one difference a ratio cannot cancel out, so it is not
allowed to exist.

One group at a time is usually what you want:

```bash
dotnet run -c Release -f net10.0 --project bench/PetToys.DbAssistant.Mssql.Benchmarks -- --filter "*WideRowBenchmarks*"
```

`--list flat` prints the benchmark names without running anything, and without
starting a container, which is the quickest way to write a filter that matches
what you meant.

Every run covers all three supported runtimes already: the configuration declares
one job per runtime, identical but for the runtime, and that is what the recorded
baseline is. Do not add `--runtimes` to get them. The switch does not clone the
configured job across the runtimes you name, it adds a *default-configured* job
for each of them beside it, so you get three runtimes measured with
BenchmarkDotNet's defaults and one measured with this project's, reported in one
table as though they were the same measurement. The `-f` above selects the
framework the runner itself is built for, which is a different thing and does not
narrow what is measured.

## Measuring against your own server

Set `MSSQL_BENCHMARK_CONNECTION_STRING` and no container is started:

```bash
MSSQL_BENCHMARK_CONNECTION_STRING="Server=db.internal;Database=scratch;User Id=loader;Password=...;TrustServerCertificate=true" dotnet run -c Release -f net10.0 --project bench/PetToys.DbAssistant.Mssql.Benchmarks -- --filter "*"
```

This is the only way to get a duration that means anything outside this
repository. A container over Docker Desktop is a fair place to compare two
versions of this library against each other and a poor stand-in for the server
you actually copy into.

The run creates its four tables in whatever database the connection string
names, dropping them first if they are already there: `narrow_row`, `wide_row`,
`source_shape_row` and `streaming_row`. Each is generated from the column list
its benchmark class declares, which is the same list every arm's mappings are
built from - one declaration, so the schema and the arms cannot drift apart. The
tables are truncated between iterations and left behind at the end. Point the
run at a scratch database.

The runner prints the database's recovery model before it measures. See
[Reading a run](#reading-a-run) for why that matters.

## What is measured

| Class                   | Arms                                                    | The question                              |
| ----------------------- | ------------------------------------------------------- | ----------------------------------------- |
| `NarrowRowBenchmarks`   | four, over four columns                                  | what the move buys on a small row         |
| `WideRowBenchmarks`     | four, over fifteen                                       | and on a wide one                         |
| `SourceShapeBenchmarks` | `IEnumerable` against `IAsyncEnumerable`                 | what the async overload adds              |
| `OffRowStreamingBenchmarks` | `EnableStreaming` on against off, over off-row values | what the library's own default buys       |
| `InRowStreamingBenchmarks`  | the same two arms over the wide row                   | what it costs when there is nothing to stream |

The four arms of a row-shape class are chosen by one question: what does a
caller have before they install this package?

| Arm                       | How it reads a value                                  | Who writes it                            |
| ------------------------- | ----------------------------------------------------- | ---------------------------------------- |
| `DataTable` **(baseline)**| values copied into `DataRow`s, then handed to the copy | anyone following the canonical snippet    |
| `Reflective reader`       | `PropertyInfo.GetValue` per value                      | someone who wants it reusable across types|
| `Hand-written reader`     | `switch` on the ordinal into direct property access    | someone who needs one entity type to be fast |
| `Mapped bulk context`     | a compiled `Func<TEntity, TProperty>` per value        | this library                              |

The `DataTable` is the baseline because it is the thing people migrate *from*,
so the ratio columns read as what the move buys rather than what the wrapper
costs. The two reader arms are kept apart deliberately: the reflective one is
the other migration source, and the hand-written one is the floor - the best a
caller could do by hand, and therefore the bound on how much of any remaining
gap can be charged to this library at all.

**The `DataTable` is built inside the timed region.** That is not an oversight,
it is the point. The claim under test is "no intermediate DataTable", and the
cost of an intermediate `DataTable` *is* its construction and its retention.
Every reader arm builds its reader inside the timed region too, because that is
what a caller does on every copy. Only the rows themselves are prepared in
`[GlobalSetup]`: they are the input all four arms share, and generating them is
nobody's cost.

All four arms write the same values, in the same order, into the same table,
through a `SqlBulkCopy` configured identically - the same options, the same
streaming flag, the same timeout, the same column mappings. That configuration
is built in one place for exactly this reason: a difference in any one setting
would silently invalidate every ratio in the report and appear nowhere in it.

Each row-shape class runs at 10,000 and 100,000 rows, and so does
`InRowStreamingBenchmarks`, whose numbers are meant to be read beside
`WideRowBenchmarks`. `OffRowStreamingBenchmarks` runs at 1,000 and 5,000, because
each of its rows carries an off-row value and a hundred thousand of those is most
of a gigabyte. The rows
come from one seeded generator and are built in `[GlobalSetup]`, so two runs of
the same revision copy identical bytes.

Two things are deliberately outside the measurement: rows carrying `null`, and
any batching or staging-table strategy. Each would be a benchmark of its own.

## Reading a run

| Column      | What it is                                                    |
| ----------- | ------------------------------------------------------------- |
| `Mean`      | The average duration of one copy of `RowCount` rows            |
| `Ratio`     | `Mean` divided by the `DataTable` arm's - below one is a saving|
| `StdDev`    | How much the copies varied; read it before quoting a mean      |
| `Allocated` | Bytes allocated per copy                                       |
| `Gen0/1/2`  | Collections per thousand copies - where retention shows        |

**`Allocated` alone understates the `DataTable`.** Every arm boxes:
`SqlBulkCopy` pulls values through `IDataRecord.GetValue(int)`, whose return type
is `object`, so a value-type column boxes once per row no matter who wrote the
reader - this library included. What differs is what happens next. A reader lets
each box die in Gen0; a `DataTable` holds every one of them, and a `DataRow` for
every row, until the copy finishes. That is retention, not allocation, and the
diagnoser reports allocated bytes rather than surviving ones. Read the `Gen1`
and `Gen2` columns beside `Allocated` or you will miss it: on the wide row at a
hundred thousand rows the `DataTable` arm promotes into both while the reader
arms promote into neither.

**There is a cost no column shows.** The `DataTable` route needs two full
materialisations alive at once - your entities, and their copy in `DataRow`s. A
reader needs one, and an `IAsyncEnumerable` source needs none. Every arm here
starts from a list that is already in memory, so the report cannot see this at
all; it is the strongest argument for the reader approach and it lives in this
paragraph rather than in a number.

**Durations are a floor.** SQL Server has no unlogged table, so the nearest way
to mute the loudest source of variance is minimal logging: the destinations are
heaps with no index or constraint, every arm copies with
`SqlBulkCopyOptions.TableLock`, and a container's database is in SIMPLE
recovery. That falls on all four arms equally, so the ratios are unaffected, but
a duration here is not what a copy into a real, indexed, fully-logged table
costs. If the runner prints a recovery model other than SIMPLE or BULK_LOGGED,
the durations include the transaction log and are not comparable with the
recorded baseline at all.

The job is `RunStrategy.Monitoring` with one invocation per iteration, because
one benchmark here is one copy of tens of thousands of rows and the table has to
be truncated between them. A run therefore collects tens of samples rather than
thousands. Warmup is five rather than BenchmarkDotNet's default of two: a freshly
started server spends its first copies filling caches, and the sibling package
recorded a phantom ratio of 1.50 by measuring them.

## Where the output lands

`BenchmarkDotNet.Artifacts/results/` beside the built benchmark assembly, which
for the commands above is
`bench/PetToys.DbAssistant.Mssql.Benchmarks/bin/Release/net10.0/`. Several
formats land there; the one that matters is `*-report-github.md`, the format
[`BASELINE.md`](BASELINE.md) is a copy of, and it opens with the processor,
operating system, SDK and runtimes of the run.

The location is pinned by the configuration rather than left at BenchmarkDotNet's
default, which is the working directory the run was launched from. Two runs
launched from two directories would otherwise leave two artifact sets that
neither overwrites nor mentions the other. The artifacts directory is
git-ignored; `BASELINE.md` is a deliberate copy kept outside it, and it is the
only run output this repository keeps.

## Comparing against the baseline

`BASELINE.md` was taken on one machine, against one server. Two rules follow:

- **Ratios travel.** Comparing `Ratio` against the baseline's is valid from any
  machine - that is the whole reason the comparison is written as one.
- **Durations do not.** `Mean` is only comparable within the environment the
  baseline was recorded in, and here that includes the server: a different
  image, a different host, a different disk, or a connection over a network
  rather than a loopback moves every number without anything in the library
  changing.

A result is not comparable at all if the run used a different job. `--job short`
and `--job dry` trade iterations for wall-clock; they are for a quick look while
working, not for a number anyone quotes.

When a change alters the copy path, re-take the baseline and commit it with that
change. Nothing enforces this: the value of a recorded baseline is exactly the
discipline of keeping it current.

## Why this is not in CI

No workflow runs this project. `build-deploy.yml` packs `src/**/*.csproj` and
never sees it; `test.yml` compiles it with the rest of the solution - which is
the point, it keeps building and keeps being analysed - and runs nothing from
it.

GitHub's hosted runners are shared, virtualised and share their disk with
whatever neighbour is on the same host. A copy benchmark there measures the
runner, not the library, and a gate on it would fail on noise - and a gate that
fails on noise gets switched off within a week, leaving the repository with a
disabled gate instead of an honest manual measurement.
