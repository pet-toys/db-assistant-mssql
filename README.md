# Database Assistant for SQL Server

[![Unit Test][test-badge]][test-url] [![NuGet Version][nuget-v-badge]][nuget-url] [![NuGet Downloads][nuget-dt-badge]][nuget-url] [![Target frameworks][dotnet-badge]][nuget-url] [![License][license-badge]][license-url]

![Database Assistant for SQL Server](https://raw.githubusercontent.com/pet-toys/db-assistant-mssql/refs/heads/dev/assets/promotion.png)

> Bulk-load your entities into SQL Server with a fluent, strongly-typed mapping
> API — no `DataTable`, no boilerplate, just `SqlBulkCopy` doing what it does
> best.

A small, focused wrapper around [`SqlBulkCopy`][sql-bulk-copy] for
`Microsoft.Data.SqlClient`. Map entity properties to table columns with
compile-checked lambda expressions and stream an `IEnumerable<TEntity>` straight
to the server through a purpose-built [`DbDataReader`][db-data-reader] — no
intermediate `DataTable`, no hand-rolled reader per table.

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

- **Fluent builder** — `CreateBulkContext` → `MapProperty` → `WriteDataAsync`.
- **Expression-based column mapping** with an optional explicit column alias.
- **Nullable-aware mapping** for both nullable value types and nullable
  reference types, driven by `NullabilityInfoContext`, with a `referenceNullable`
  fallback for models compiled without a nullable context.
- **Streaming writer** — values flow through a purpose-built `DbDataReader`
  instead of an intermediate `DataTable`.
- **Managed connection lifecycle** — a closed connection is opened for the copy
  and closed again afterwards, leaving it as it was found.
- **Transactions and cancellation** — pass a `SqlTransaction` and a
  `CancellationToken` to `WriteDataAsync`.
- **Tunable copy** — configure `SqlBulkCopyOptions`, streaming, and the bulk
  copy timeout through `SqlBulkOptions`; the call returns the number of rows
  copied.
- **Broad type support** — `bool`, `char`, `string`, `byte`, `short`, `int`,
  `long`, `float`, `double`, `decimal`, `DateTime`, `Guid`, `byte[]`, and
  `char[]`.
- **Multi-targets** `net8.0`, `net9.0`, and `net10.0`.

## Installation

```sh
dotnet add package PetToys.DbAssistant.Mssql
```

## Getting started

The examples below map the same entity the test suite uses — a class with a
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
instead — it defaults to `true`:

```csharp
// NullableDisabledEntity is declared under #nullable disable.
await connection.CreateBulkContext<NullableDisabledEntity>("Records")
    .MapProperty(e => e.Int0)
    .MapProperty(e => e.Str1, referenceNullable: true)   // nullable column
    .MapProperty(e => e.Str0, referenceNullable: false)  // NOT NULL column
    .WriteDataAsync(records);
```

The flag is ignored for value types, whose nullability is always known.

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
        options.EnableStreaming = true;                        // default
    });
```

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
- **Unsupported property types fail fast.** Mapping a property whose type is not
  in the supported list throws an `InvalidOperationException` when the mapping is
  built, not midway through the copy.
- **Load into a staging table for the best throughput.** Bulk-copy into a
  heap-style temporary or staging table with no indexes or keys, then insert
  from there into the indexed target table. This keeps the copy itself as cheap
  as possible.

More runnable examples live in the [unit tests][tests-url].

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
