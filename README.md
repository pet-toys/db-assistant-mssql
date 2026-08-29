# Database Assistant for SQL Server

[![Unit Test][test-badge]][test-url] [![NuGet Version][nuget-v-badge]][nuget-url] [![NuGet Downloads][nuget-dt-badge]][nuget-url] [![Target frameworks][dotnet-badge]][nuget-url] [![License][license-badge]][license-url]

![Database Assistant for SQL Server](https://raw.githubusercontent.com/pet-toys/db-assistant-mssql/refs/heads/dev/assets/promotion.png)

> Bulk-load your entities into SQL Server with a fluent, strongly-typed mapping
> API: no `DataTable`, no boilerplate, just `SqlBulkCopy` doing what it does
> best.

A small, focused wrapper around [`SqlBulkCopy`][sql-bulk-copy] for
`Microsoft.Data.SqlClient`. Map entity properties to table columns with
compile-checked lambda expressions and stream an `IEnumerable<TEntity>`, or an
`IAsyncEnumerable<TEntity>`, straight to the server through a purpose-built
[`DbDataReader`][db-data-reader]: no intermediate `DataTable`, no hand-rolled
reader per table.

## Why

`SqlBulkCopy` is the fastest way to push many rows into SQL Server, but its API
is built around `DataTable` or a manually implemented `IDataReader`. Filling a
`DataTable` copies every value into an intermediate buffer before the copy even
starts; writing your own reader for each entity is tedious and easy to get
wrong. This library closes that gap:

- **Skip the `DataTable`.** Entities are streamed row by row through a custom
  reader, so a million-row insert does not first materialize a million-row table
  in memory.
- **Map with expressions, not magic strings.** `MapProperty(e => e.Int0)` is
  checked by the compiler and survives a rename; a different column name can
  still be supplied explicitly when the table and the model disagree.
- **Let nullability just work.** The mapper reads the nullable annotations of
  your model, so nullable value types and nullable reference types map to
  nullable columns without extra ceremony.
- **Stay in control.** Wrap the copy in a transaction, set a timeout, or pass
  through any `SqlBulkCopyOptions` when you need to.

## Features

- **Fluent builder**: `CreateBulkContext` → `MapProperty` → `WriteDataAsync`.
- **Expression-based column mapping** with an optional explicit column alias.
- **Nullable-aware mapping** for both nullable value types and nullable
  reference types, driven by `NullabilityInfoContext`, with a `referenceNullable`
  fallback for models compiled without a nullable context.
- **Streaming writer**: values flow through a purpose-built `DbDataReader`
  instead of an intermediate `DataTable`.
- **Asynchronous sources**: pass an `IAsyncEnumerable<TEntity>` and rows are
  pulled as the copy consumes them, so a producer that is itself asynchronous
  (another database, an HTTP API, a queue) is never collected into a list first.
- **Managed connection lifecycle**: a closed connection is opened for the copy
  and closed again afterwards, leaving it as it was found.
- **Transactions and cancellation**: pass a `SqlTransaction` and a
  `CancellationToken` to `WriteDataAsync`.
- **Tunable copy**: configure `SqlBulkCopyOptions`, streaming, and the bulk
  copy timeout through `SqlBulkOptions`; the call returns the number of rows
  copied.
- **Broad type support**: `bool`, `char`, `string`, `byte`, `short`, `int`,
  `long`, `float`, `double`, `decimal`, `Guid`, `byte[]`, `char[]`, and the
  date and time family in full: `DateTime`, `DateTimeOffset`, `TimeSpan`,
  `DateOnly`, and `TimeOnly`.
- **Multi-targets** `net8.0`, `net9.0`, and `net10.0`.

## Installation

```sh
dotnet add package PetToys.DbAssistant.Mssql
```

## Getting started

The examples below map the same entity the test suite uses, a class with a
spread of supported types, including nullable value and reference types:

```csharp
public sealed class NullableEnabledEntity
{
    public int Int0 { get; init; }
    public int? Int1 { get; init; }
    public DateTime Date0 { get; init; }
    public DateTime? Date1 { get; init; }
    public string Str0 { get; init; } = string.Empty;
    public string? Str1 { get; init; }
    public byte[] Arr0 { get; init; } = [];
    public byte[]? Arr1 { get; init; }
}
```

Open a `SqlConnection`, describe how the entity maps to the target table, and
write the data:

```csharp
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Extensions;

await using var connection = new SqlConnection(connectionString);

long rowsCopied = await connection.CreateBulkContext<NullableEnabledEntity>("Records")
    .MapProperty(e => e.Int0)
    .MapProperty(e => e.Int1)
    .MapProperty(e => e.Date0)
    .MapProperty(e => e.Str0)
    .MapProperty(e => e.Arr0)
    .WriteDataAsync(records);
```

`WriteDataAsync` opens the connection if it is closed, performs the bulk copy,
closes the connection again if it opened it, and returns the number of rows
copied.

## Usage

### Mapping properties

Each `MapProperty` call adds one column to the copy. By default the database
column takes the property's name, so the lambda is all you need when the model
and the table line up:

```csharp
long rowsCopied = await connection.CreateBulkContext<NullableEnabledEntity>("Records")
    .MapProperty(e => e.Int0)   // -> column "Int0"
    .MapProperty(e => e.Date0)  // -> column "Date0"
    .WriteDataAsync(records);
```

A property and a column may each be mapped only once. Mapping the same property
twice, or two properties onto the same column, throws an
`InvalidOperationException`.

### Column aliases

When the column name differs from the property name, pass it explicitly:

```csharp
await connection.CreateBulkContext<NullableEnabledEntity>("Records")
    .MapProperty(e => e.Int1, "alias")  // -> column "alias"
    .WriteDataAsync(records);
```

### Entities without a nullable context

For reference-typed properties the mapper consults the model's nullable
annotations to decide whether the column accepts `NULL`. If the entity was
compiled without a nullable context (for example under `#nullable disable`),
that information is unavailable, and the `referenceNullable` flag supplies it
instead. It defaults to `true`:

```csharp
// NullableDisabledEntity is declared under #nullable disable.
await connection.CreateBulkContext<NullableDisabledEntity>("Records")
    .MapProperty(e => e.Int0)
    .MapProperty(e => e.Str1, referenceNullable: true)   // nullable column
    .MapProperty(e => e.Str0, referenceNullable: false)  // NOT NULL column
    .WriteDataAsync(records);
```

The flag is ignored for value types, whose nullability is always known.

### Streaming from an asynchronous source

When the rows themselves arrive asynchronously, pass the
`IAsyncEnumerable<TEntity>` directly. It is enumerated once and lazily: the
reader holds a single row at a time and keeps the producer exactly one row ahead
of the copy, so a source larger than memory never has to be materialized:

```csharp
static async IAsyncEnumerable<NullableEnabledEntity> ReadPagesAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    for (var page = 0; ; page++)
    {
        var batch = await FetchPageAsync(page, cancellationToken);
        if (batch.Count == 0) yield break;

        foreach (var record in batch) yield return record;
    }
}

long rowsCopied = await connection.CreateBulkContext<NullableEnabledEntity>("Records")
    .MapProperty(e => e.Int0)
    .MapProperty(e => e.Date0)
    .WriteDataAsync(ReadPagesAsync(), cancellationToken: cancellationToken);
```

The overload is otherwise identical to the synchronous one: same options, same
transaction, same row count. The `cancellationToken` is handed to
`GetAsyncEnumerator`, so a producer written as an asynchronous iterator observes
the cancellation as well as the copy does.

A source whose type implements *both* `IEnumerable<TEntity>` and
`IAsyncEnumerable<TEntity>` (Entity Framework Core's `DbSet<TEntity>`, for
example) matches both overloads equally, and the compiler cannot pick. Say
which one you mean:

```csharp
await context.WriteDataAsync((IAsyncEnumerable<Record>)dbContext.Set<Record>());
```

### Tuning the bulk copy

Pass a configuration delegate to adjust the underlying `SqlBulkCopy`:

```csharp
await connection.CreateBulkContext<NullableEnabledEntity>("Records")
    .MapProperty(e => e.Int0)
    .MapProperty(e => e.Date0)
    .WriteDataAsync(records, options =>
    {
        options.BulkCopyTimeout = 60;                          // seconds; 0 = no limit (default)
        options.CopyOptions = SqlBulkCopyOptions.TableLock;
        options.EnableStreaming = true;                        // off by default
    });
```

`EnableStreaming` is off, which is `SqlBulkCopy`'s own default. Turn it on
when your rows carry a `varchar(max)`, `nvarchar(max)` or `varbinary(max)`
column whose values are large enough that SQL Server stores them off-row:
streaming writes such a value without holding it in memory, and that is the
case the flag exists for. Where every value fits in-row it has nothing to
stream and still pays for the machinery, an allocation per column on every
row, so off is the cheaper default for ordinary rows.

### Transactions and cancellation

Enlist the copy in a transaction and cancel it through a token:

```csharp
await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

await connection.CreateBulkContext<NullableEnabledEntity>("Records")
    .MapProperty(e => e.Int0)
    .MapProperty(e => e.Date0)
    .WriteDataAsync(records, transaction: transaction, cancellationToken: cancellationToken);

await transaction.CommitAsync(cancellationToken);
```

## Good to know

- **The connection is left as it was found.** A connection that is already open
  stays open; a closed one is opened for the copy and closed again afterwards.
  With your own transaction, open the connection yourself and keep it open for
  the lifetime of the transaction.
- **Timeout defaults to no limit.** `BulkCopyTimeout` defaults to `0`, which
  means the copy waits indefinitely. Set it when you want a ceiling.
- **An asynchronous source is read one row ahead.** The next row is pulled while
  the current one is being copied, and the first one before the copy starts, so
  a producer with side effects (acknowledging a queue message as it yields it)
  can consume one row more than the copy writes when the write fails or is
  cancelled. `BulkCopyTimeout` does not bound the wait for that first row;
  cancel through the token instead.
- **Unsupported property types fail fast.** Mapping a property whose type is not
  in the supported list throws an `InvalidOperationException` when the mapping is
  built, not midway through the copy.
- **The destination column's type is yours to get right.** The library does not
  know it and does not check it: a supported property type is written as it
  stands, and a column that cannot hold it is reported by the server when the
  copy runs. `TimeSpan` is the one worth naming, because SQL Server's `time` is a
  time of day, bounded to under 24 hours and never negative, while a `TimeSpan`
  is a duration and can be either. `TimeOnly` states the narrower meaning at the
  call site; a real duration belongs in a numeric column.
- **Load into a staging table for the best throughput.** Bulk-copy into a
  heap-style temporary or staging table with no indexes or keys, then insert
  from there into the indexed target table. This keeps the copy itself as cheap
  as possible.

More runnable examples live in the [unit tests][tests-url].

## Performance

Measured rather than asserted. The benchmark project is in
[`bench/`](bench/PetToys.DbAssistant.Mssql.Benchmarks), the recorded throughput
run in [`BASELINE.md`](bench/PetToys.DbAssistant.Mssql.Benchmarks/BASELINE.md)
and the recorded capacity run in
[`CAPACITY.md`](bench/PetToys.DbAssistant.Mssql.Benchmarks/CAPACITY.md). Both were
taken on one machine against one containerised server: the ratios below travel,
the durations and row counts behind them do not.

**More of your rows fit in the same process.** The mainstream approach copies
every row into a `DataTable` first, so a caller who has just deserialised a
request body holds their data twice. This library holds it once and streams the
second copy a row at a time. Under a fixed heap ceiling, with a distinct value per
row, that was 1.93x as many four-column rows and 1.25x as many fifteen-column
ones.

The spread is the point, and it is a rule rather than a range: a `DataTable` adds
row bookkeeping plus a boxed copy of every value-typed column, which is roughly
fixed per column and does not grow with the size of the values, while your own
rows weigh whatever your data weighs. So the gain is largest where rows are small
and numerous, and it approaches nothing where they are large. `CAPACITY.md` gives
the arithmetic to apply to your own row instead of guessing which measured shape
it resembles.

**The same factor decides how many requests fit at once.** A batch endpoint under
load is not one large copy, it is many copies on one instance, and one instance is
one managed heap. Holding the row count per copy fixed and counting how many
simultaneous copies survive gives back the same ratio: 2.17x on the four-column
row and 1.25x on the fifteen-column one, against 1.93x and 1.25x for the single
copy. So the number you work out from the rule above is also how many times more
concurrent requests one instance of your service will take.

**A hand-written reader's speed, without the hand-written reader.** The benchmark
includes an `IDataReader` written for exactly one entity type, switching on the
ordinal into direct property access: the best a caller could reasonably do by
hand. The mapped bulk context lands within a few percent of it, and which of the
two is ahead changes between runs.

**A single `DataTable` copy is faster.** Copying a hundred thousand rows, the
mapped context took 1.30x longer on the wide shape and 1.62x longer on the narrow
one. Building a `DataTable` is cheaper per row than pulling values through a
reader, and `SqlBulkCopy` does not take the same path for the two sources.

The trade, then: a copy in isolation costs more time, and more of them fit at
once. Small occasional copies do not need this package for throughput. Large or
concurrent ones are bounded by the ceiling rather than by the clock.

## License

Provided under the [Apache License, Version 2.0][license-url].

[test-badge]: https://img.shields.io/github/actions/workflow/status/pet-toys/db-assistant-mssql/test.yml?branch=dev&style=flat-square&logo=github&label=test
[test-url]: https://github.com/pet-toys/db-assistant-mssql/actions?query=workflow%3Atest+branch%3Adev
[nuget-v-badge]: https://img.shields.io/nuget/v/PetToys.DbAssistant.Mssql?style=flat-square&logo=nuget&label=version
[nuget-dt-badge]: https://img.shields.io/nuget/dt/PetToys.DbAssistant.Mssql?style=flat-square&logo=nuget
[nuget-url]: https://www.nuget.org/packages/PetToys.DbAssistant.Mssql/
[dotnet-badge]: https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4?style=flat-square&logo=dotnet
[license-badge]: https://img.shields.io/github/license/pet-toys/db-assistant-mssql?style=flat-square&color=blue
[license-url]: https://www.apache.org/licenses/LICENSE-2.0
[sql-bulk-copy]: https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlbulkcopy
[db-data-reader]: https://learn.microsoft.com/dotnet/api/system.data.common.dbdatareader
[tests-url]: https://github.com/pet-toys/db-assistant-mssql/tree/dev/test/PetToys.DbAssistant.Mssql.Test
