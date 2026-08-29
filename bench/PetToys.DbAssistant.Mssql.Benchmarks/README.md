# Benchmarks

Two measurements of `PetToys.DbAssistant.Mssql` against the alternatives a
caller has without it:

- **Throughput and allocation** per copy, in BenchmarkDotNet. Recorded in
  [`BASELINE.md`](BASELINE.md).
- **Capacity**: how many rows fit under a fixed heap ceiling. Recorded in
  [`CAPACITY.md`](CAPACITY.md).

Nothing here is a gate. No build, pull request or release fails because of this
project; see [Why this is not in CI](#why-this-is-not-in-ci).

## What the recorded run found

Wide row, 100,000 rows, .NET 10, `DataTable` as the baseline:

| Arm                 |     Mean | Ratio | Allocated | Alloc ratio |
| ------------------- | -------: | ----: | --------: | ----------: |
| `DataTable`         | 702.2 ms |  1.00 | 122.45 MB |        1.00 |
| Reflective reader   | 983.4 ms |  1.40 |  88.79 MB |        0.73 |
| Hand-written reader | 889.0 ms |  1.27 |  88.77 MB |        0.72 |
| Mapped bulk context | 910.1 ms |  1.30 |  88.87 MB |        0.73 |

Three results follow from the full set, and two of them are unfavourable:

1. **A single `DataTable` copy is faster.** 1.30x on the wide row and 1.62x on
   the narrow one, with `EnableStreaming` at its current default. The three
   reader arms are within a few percent of each other, so the gap is in
   `SqlBulkCopy`'s reader path rather than in any mapping layer.
2. **Reflection is not the cost.** The reflective reader, the hand-written
   reader and this library's compiled accessors land within about a tenth of
   each other, and their ordering changes between runs and row shapes: on the
   narrow row the reflective arm was the fastest of the three. A copy is
   dominated by `SqlBulkCopy` and the server, not by how a value is read out of
   an object.
3. **Capacity is where the difference is.** Under a fixed heap ceiling the
   mapped context copies 1.93x as many narrow rows and 1.25x as many wide ones,
   because it holds the caller's collection once rather than twice. See
   [`CAPACITY.md`](CAPACITY.md).

All of the above is with `EnableStreaming` off, which became the default in
release 10.4.0 to match `SqlBulkCopy`. It was previously on, at a cost of one
allocation per column per row. `InRowStreamingBenchmarks` prices that directly on
the same shape and row count: 1,883.1 ms and 632 MB with it on against 1,209.0 ms
and 88.86 MB with it off.

## Running it

From the repository root. The project multi-targets, so a framework has to be
named:

```bash
dotnet run -c Release -f net10.0 --project bench/PetToys.DbAssistant.Mssql.Benchmarks -- --filter "*"
```

The run needs a Docker engine that can run Linux containers, the same one the
integration tests need. It starts a single `mcr.microsoft.com/mssql/server`
container and stops it at the end. The image is around 1.5 GB and wants roughly
2 GB of memory, so a first run spends a while pulling.

To measure one group:

```bash
dotnet run -c Release -f net10.0 --project bench/PetToys.DbAssistant.Mssql.Benchmarks -- --filter "*WideRowBenchmarks*"
```

`--list flat` prints the benchmark names without running anything and without
starting a container.

### The container is started by the runner

`Program` starts it and passes it to the benchmark processes through
`MSSQL_BENCHMARK_CONNECTION_STRING`. It is not started from `[GlobalSetup]`.

BenchmarkDotNet gives every case (each method at each row count) its own
process, so a container started from the setup is one container per case, and
the two arms of a ratio would be measured against different servers. A ratio
cannot cancel that out.

### Do not pass `--runtimes`

Every run already covers all three supported runtimes: the configuration
declares one job per runtime, identical except for the runtime, and that is what
`BASELINE.md` records.

`--runtimes` does not clone the configured job across the runtimes named. It
adds a *default-configured* job for each of them alongside the existing ones, so
the report mixes three runtimes measured with BenchmarkDotNet's defaults and
three measured with this project's, and the `Ratio` column then compares
runtimes rather than arms.

`-f` above selects the framework the runner itself is built for. It does not
narrow what is measured.

## Measuring against your own server

Set `MSSQL_BENCHMARK_CONNECTION_STRING` and no container is started:

```bash
MSSQL_BENCHMARK_CONNECTION_STRING="Server=db.internal;Database=scratch;User Id=loader;Password=...;TrustServerCertificate=true" dotnet run -c Release -f net10.0 --project bench/PetToys.DbAssistant.Mssql.Benchmarks -- --filter "*"
```

This is the only way to get a duration that means anything outside this
repository. A container over Docker Desktop is adequate for comparing two
revisions of this library against each other and a poor stand-in for the server
you copy into.

The run creates five tables in whatever database the connection string names,
dropping them first if present: `narrow_row`, `wide_row`, `source_shape_row`,
`streaming_row` and `in_row_streaming_row`. The capacity probe adds
`probe_narrow_row` and `probe_wide_row`. Each is generated from the column list
its class declares, which is the same list every arm's mappings are built from,
so the schema and the arms cannot drift apart. Tables are truncated between
iterations and left behind at the end. Point the run at a scratch database.

The runner prints the database's recovery model before measuring. See
[Reading a run](#reading-a-run).

## What is measured

| Class                       | Arms                                                    | Question                                      |
| --------------------------- | ------------------------------------------------------- | --------------------------------------------- |
| `NarrowRowBenchmarks`       | four, over four columns                                  | what the move buys on a small row             |
| `WideRowBenchmarks`         | four, over fifteen                                       | and on a wide one                             |
| `SourceShapeBenchmarks`     | `IEnumerable` against `IAsyncEnumerable`                 | what the async overload adds                  |
| `OffRowStreamingBenchmarks` | `EnableStreaming` off against on, over off-row values    | what turning it on buys                       |
| `InRowStreamingBenchmarks`  | the same two arms over the wide row                      | what it costs with nothing to stream          |

The four arms of a row-shape class answer one question: what does a caller have
before they install this package?

| Arm                        | How it reads a value                                   | Who writes it                                |
| -------------------------- | ------------------------------------------------------ | -------------------------------------------- |
| `DataTable` **(baseline)** | values copied into `DataRow`s, then handed to the copy  | anyone following the canonical snippet       |
| `Reflective reader`        | `PropertyInfo.GetValue` per value                       | someone who wants it reusable across types   |
| `Hand-written reader`      | `switch` on the ordinal into direct property access     | someone who needs one entity type to be fast |
| `Mapped bulk context`      | a compiled `Func<TEntity, TProperty>` per value         | this library                                 |

`DataTable` is the baseline because it is what callers migrate from, so the
ratio columns read as what the move costs or buys. The two reader arms are kept
separate: the reflective one is the other migration source, and the hand-written
one is the floor, which bounds how much of any remaining gap is attributable to
this library.

Each row-shape class runs at 10,000 and 100,000 rows, as does
`InRowStreamingBenchmarks`, whose numbers are read beside `WideRowBenchmarks`.
`OffRowStreamingBenchmarks` runs at 1,000 and 5,000: each of its rows carries an
off-row value, and a hundred thousand of those is most of a gigabyte.

Rows come from one seeded generator and are built in `[GlobalSetup]`, so two
runs of the same revision copy identical bytes.

Outside the measurement, deliberately: rows carrying `null`, and any batching or
staging-table strategy. Each would be a benchmark of its own.

### Conditions the arms share

**The `DataTable` is built inside the timed region.** The claim under test is
"no intermediate `DataTable`", so the cost of building and holding one is the
quantity being measured. Every reader arm builds its reader inside the timed
region too, because a caller does that on every copy. Only the rows are prepared
in `[GlobalSetup]`; they are the input all four arms share.

**The `DataTable` is presized to the row count.** `DataRowCollection` otherwise
grows by doubling from a small initial capacity, which would charge the baseline
for its own regrowth. The caller this arm represents holds a materialised
collection and knows its count.

**Every arm copies through an identically configured `SqlBulkCopy`**: the same
options, streaming flag, timeout and column mappings, built in one place
(`CopySettings`). A difference in any one setting would invalidate every ratio
in the report without appearing in it.

## Reading a run

| Column      | Meaning                                                        |
| ----------- | -------------------------------------------------------------- |
| `Mean`      | Average duration of one copy of `RowCount` rows                 |
| `Ratio`     | `Mean` divided by the `DataTable` arm's; below one is a saving  |
| `StdDev`    | Spread across iterations; read it before quoting a mean         |
| `Allocated` | Bytes allocated per copy                                        |
| `Gen0/1/2`  | Collections per thousand copies                                 |

**`Allocated` measures allocation, not retention.** Every arm boxes:
`SqlBulkCopy` pulls values through `IDataRecord.GetValue(int)`, which returns
`object`, so a value-type column boxes once per row regardless of who wrote the
reader. What differs is lifetime. A reader lets each box die in Gen0; a
`DataTable` holds every box and a `DataRow` per row until the copy finishes.

The two do not rank the arms the same way. On the wide row the reader arms
allocate a little under three quarters of what the `DataTable` arm allocates and
retain none of it; on the narrow row they allocate half again as much and still
retain none of it.

**The generation counters are not a reliable proxy for retention.** Presizing
the `DataTable` removed its `Gen2` promotions on the narrow row entirely, on all
three runtimes, without changing what it holds. A promotion counter indicates
churn, not survival. Capacity is measured directly instead; see
[Capacity](#capacity-how-many-rows-fit).

**The streaming flag reaches three arms, not four.** All four are configured
identically, `EnableStreaming` included, but a `DataTable` source does not take
the path the flag changes. Flipping it moved the `DataTable` arm's allocation by
under a tenth of a percent on both row shapes and the reader arms' by a factor
of seven.

**Durations are a floor.** SQL Server has no unlogged table, so the nearest way
to mute the loudest source of variance is minimal logging: destinations are
heaps with no index or constraint, every arm copies with
`SqlBulkCopyOptions.TableLock`, and a container's database is in SIMPLE
recovery. This applies to all four arms equally, so ratios are unaffected, but a
duration here is not what a copy into a real, indexed, fully-logged table costs.
If the runner reports a recovery model other than SIMPLE or BULK_LOGGED, the
durations include the transaction log and are not comparable with `BASELINE.md`.

**Job settings.** `RunStrategy.Monitoring`, one invocation per iteration:
one benchmark here is one copy of tens of thousands of rows, and the table is
truncated between them, so a run collects tens of samples rather than thousands.
Warmup is five rather than BenchmarkDotNet's default of two, because a freshly
started server spends its first copies filling caches; the sibling package
recorded a phantom ratio of 1.50 by measuring them.

## Where the output lands

`BenchmarkDotNet.Artifacts/results/` beside the built benchmark assembly, which
for the commands above is
`bench/PetToys.DbAssistant.Mssql.Benchmarks/bin/Release/net10.0/`. Several
formats land there; `*-report-github.md` is the one `BASELINE.md` is assembled
from, and it opens with the processor, operating system, SDK and runtimes.

The location is pinned by the configuration rather than left at BenchmarkDotNet's
default of the launch directory, so two runs launched from two directories do not
leave two artifact sets that neither overwrites nor mentions the other. The
artifacts directory is git-ignored. `BASELINE.md` and `CAPACITY.md` are copies
kept outside it, and are the only run output this repository keeps.

## Comparing against the baseline

`BASELINE.md` was taken on one machine against one server:

- **Ratios travel.** Comparing `Ratio` against the baseline's is valid from any
  machine.
- **Durations do not.** `Mean` is comparable only within the environment it was
  recorded in, and here that includes the server: a different image, host, disk,
  or a connection over a network rather than a loopback moves every number
  without anything in the library changing.

A result is not comparable at all if the run used a different job. `--job short`
and `--job dry` trade iterations for wall-clock and are for working, not for
quoting.

When a change alters the copy path, re-take the baseline and commit it with that
change. Nothing enforces this.

## Capacity: how many rows fit

The tables above measure copies that complete. A copy that runs out of memory
does not appear in them at all, and neither `Allocated` nor the generation
counters stand in for it, for the reasons in [Reading a run](#reading-a-run).

The probe measures it directly: under a fixed managed heap ceiling, what is the
largest row count each mechanism can copy?

```bash
dotnet run -c Release -f net10.0 --project bench/PetToys.DbAssistant.Mssql.Benchmarks -- --probe --shape narrow --heap 256
```

`--shape` takes `narrow` or `wide`, `--heap` a ceiling in megabytes, and
`--workstation-gc` switches the GC mode the attempts run under. The result is in
[`CAPACITY.md`](CAPACITY.md).

**How it works.** Each attempt is a child process started with
`DOTNET_GCHeapHardLimit` and the GC mode set explicitly, and its exit status is
the only signal read: a process that has exhausted its heap cannot report on
itself. The probe doubles the row count until an attempt fails, bisects between
the last success and the first failure, and reports the largest count that
succeeded twice. A single success at the boundary is decided by GC timing. A
child that hangs, or that fails for a reason other than memory, fails the whole
run rather than being recorded as either outcome.

**It reports no duration.** That is the benchmark's subject, measured there
under conditions chosen to make it comparable.

**Its numbers travel worse than durations.** A boundary moves with the ceiling,
the GC mode, the pointer size, the runtime and fragmentation, and is not
reproducible to better than roughly a tenth between two runs on one machine.
What travels is the ratio between mechanisms in one run, and beyond that the
rule `CAPACITY.md` derives from four measured points.

**Both source weights are measured.** The caller's collection is a cost both
mechanisms pay, so its weight is what the ratio divides by. `RowSet` draws text
from a pool by default, which keeps row generation from dominating a throughput
benchmark and would flatter the probe; its `shareText` parameter gives each row
its own values. The row shape, columns and mappings are unchanged either way.

`--probe` is the only entry point. A benchmark invocation starts no attempt
whatever its filter, and `--probe-attempt` is how the probe starts its own
children.

## Why this is not in CI

No workflow runs this project. `build-deploy.yml` packs `src/**/*.csproj` and
never sees it; `test.yml` compiles it with the rest of the solution, which keeps
it building and analysed, and runs nothing from it.

GitHub's hosted runners are shared, virtualised, and share a disk with whatever
else is on the host. A copy benchmark there measures the runner rather than the
library, and a gate on it would fail on noise.

The probe additionally kills processes on purpose and depends on a heap ceiling
that a shared virtualised host cannot honour in a way that would make a boundary
mean anything.
