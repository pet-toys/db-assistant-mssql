# Capacity

How many rows fit, rather than how fast they copy. Recorded by the capacity
probe; see [README.md](README.md) for what it does and why it is not a
benchmark.

**Only the ratio travels.** The absolute row counts belong to this machine, this
ceiling, this GC mode and this runtime, and they move with fragmentation. What
carries elsewhere is the factor between the two mechanisms, and the rule at the
bottom of this file carries further than either.

## Environment

Heap ceiling 256 MB, server GC, 64-bit, .NET 10.0.11, against a
`mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` container started by the
run. Every reported row count fitted twice; a single success at the boundary is
decided by GC timing rather than by capacity.

## Result

| Row shape | The caller's own rows | `DataTable` | Mapped bulk context | Ratio |
| --------- | --------------------- | ----------: | ------------------: | ----: |
| Narrow, 4 columns  | text shared from a pool | 975,000 | 4,175,000 | 4.28x |
| Narrow, 4 columns  | a distinct value per row | 675,000 | 1,300,000 | 1.93x |
| Wide, 15 columns   | text shared from a pool | 450,000 | 1,225,000 | 2.72x |
| Wide, 15 columns   | a distinct value per row | 100,000 | 125,000 | 1.25x |

Both source weights are recorded because the caller's own collection is a cost
both mechanisms pay, so what it weighs decides how much of the ceiling is left
for anything else. Neither row is the answer; the two bracket where a real
caller falls.

**The ratio is not stable to the last percent.** Three runs of the narrow row
gave 4.81x, 4.28x and 4.39x on shared text, and 1.93x and 2.04x on distinct text.
The boundary is decided by when a collection happens to fall, and confirming a
row count twice removes a coin flip without making the figure reproducible to
better than roughly a tenth. Read these as one significant figure.

## The rule behind the numbers

The four rows are not four measurements. They are one relationship measured at
four points:

```
ratio  ~=  1 + (90 + 24 x value-typed columns) / bytes per row of your own entity
```

A `DataTable` adds `DataRow` bookkeeping plus one boxed copy of every value-typed
column. That addition is roughly fixed per column and does not grow with the size
of the values: a `string` or a `byte[]` column costs it a reference, because the
value is already an object. Your own rows, meanwhile, weigh whatever your data
weighs.

So the advantage is largest exactly where rows are small and numerous, and it
falls towards nothing as rows grow large:

| Your row | Predicted | Measured |
| -------- | --------: | -------: |
| Narrow, pooled text: ~48 B    | 4.38x | 4.28x |
| Wide, pooled text: ~152 B     | 3.01x | 2.72x |
| Narrow, distinct text: ~144 B | 2.13x | 1.93x |
| Wide, distinct text: ~1440 B  | 1.21x | 1.25x |

The rule is worth more than any single number in the table above it, because a
reader can apply it to their own row instead of guessing which of these they
resemble.

## What this does not measure

- Time. Deliberately: it is the benchmark project's subject, and
  [`BASELINE.md`](BASELINE.md) says plainly that a single `DataTable` copy is the
  faster one.
- Concurrent copies. The factor multiplies across simultaneous copies on one
  instance, which is the closer model of a burst, but a concurrency figure is
  read against a known single-copy ceiling and that is what this file is.
- Anything about a source that is not already materialised. Both mechanisms here
  are handed a collection the caller has already paid for, because a request body
  deserialised into memory is the situation this package was written for.
