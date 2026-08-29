# Capacity

How many rows fit in one copy, and how many copies fit at once, rather than how
fast either goes. Recorded by the capacity probe; see [README.md](README.md) for
what it does and why it is not a benchmark.

**Only the ratio travels.** The absolute row counts and copy counts belong to
this machine, this ceiling, this GC mode and this runtime, and they move with
fragmentation. What carries elsewhere is the factor between the two mechanisms,
and the rule at the bottom of this file carries further than either.

## Environment

Heap ceiling 256 MB, server GC, 64-bit, .NET 10.0.11, against a
`mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` container started by the
run. Every reported figure fitted twice; a single success at the boundary is
decided by GC timing rather than by capacity.

Both results below were taken under those conditions, so they can be read
against each other. The concurrency figures name their rows per copy as well,
because a count of copies means nothing without the size of one.

## Result: how many rows in one copy

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

## Result: how many copies at once

One copy is not the situation this package was written for. A batch endpoint
takes many requests at a time, each already deserialised into memory, and one
instance is one managed heap. With the row count per copy fixed, how many
simultaneous copies survive?

| Row shape | Rows per copy | The caller's own rows | `DataTable` | Mapped bulk context | Ratio |
| --------- | ------------: | --------------------- | ----------: | ------------------: | ----: |
| Narrow, 4 columns | 100,000 | text shared from a pool  |  9 | 44 | 4.89x |
| Narrow, 4 columns | 100,000 | a distinct value per row |  6 | 13 | 2.17x |
| Wide, 15 columns  |  25,000 | text shared from a pool  | 17 | 47 | 2.76x |
| Wide, 15 columns  |  25,000 | a distinct value per row |  4 |  5 | 1.25x |

**The factor carries into a burst unchanged.** Set beside the single-copy table
above, taken at the same ceiling on the same shapes and source weights:

| Row shape and weight | One copy | Many copies |
| -------------------- | -------: | ----------: |
| Narrow, pooled text   | 4.28x | 4.89x |
| Narrow, distinct text | 1.93x | 2.17x |
| Wide, pooled text     | 2.72x | 2.76x |
| Wide, distinct text   | 1.25x | 1.25x |

So the ratio a reader computes from the rule below is the ratio they get on a
burst as well: this library holds about as many times more simultaneous requests
as it held times more rows in one of them. That is what `CAPACITY.md` used to
assert without evidence.

The two columns differ by at most a seventh, and in the same direction each time,
which is inside the spread the single-copy figures have between runs of their own.

**A copy count is a small integer, so its ratio is coarser than a row count's.**
At nine copies against forty-four, one copy either way moves the ratio by about
a seventh; the same run at 200,000 rows per copy halves both counts, to four
against twenty-two, and reports 5.50x for the same underlying fact. The rows per
copy are named in every row above for that reason, and the ratio is worth one
significant figure and no more.

**Doubling the rows per copy halves the counts**, which is the check that the
figures are a capacity rather than a scheduling artefact. Narrow, from 100,000
to 200,000 rows per copy: 9 to 4 and 44 to 22 on pooled text, 6 to 3 and 13 to 6
on distinct text.

### How the copies are arranged, and why it matters

Each copy runs on a thread of its own so that all of them are genuinely in
flight at once. Started from one thread instead, they run one at a time up to
their first `await`, and the `DataTable` route materialises its whole table
before it awaits anything: the first copies would be writing, and freeing their
tables, while the last were still building theirs. That version of the probe
reported sixteen `DataTable` copies where nine fit, because sixteen never
coexisted. The peak was being set by the server's speed against the client's,
which is a duration wearing a capacity's clothes.

Each copy also writes to a table of its own. Copies into one table are what a
real caller does and are legal against a heap under `TABLOCK`, but a lock wait
would put the boundary where the server's locking falls rather than where the
client's memory does, and a deadlock exits non-zero, which the probe reads as
"did not fit" - a wrong answer rather than a missing one.

## The rule behind the numbers

The four rows of the single-copy table are not four measurements. They are one
relationship measured at four points:

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
resemble. It carries to a burst as well: the concurrency table reproduces the
ratio this rule predicts, at all four points, so a reader who works out their own
factor has worked out how many times more simultaneous requests one instance
holds.

## What this does not measure

- Time. Deliberately: it is the benchmark project's subject, and
  [`BASELINE.md`](BASELINE.md) says plainly that a single `DataTable` copy is the
  faster one.
- How many simultaneous bulk loads one SQL Server instance sustains. That is a
  question about a server, and the container this run starts is not the server
  anybody copies into. The concurrency table above is about the client's memory,
  which is why every copy in it writes to a table of its own.
- Anything about a source that is not already materialised. Both mechanisms here
  are handed a collection the caller has already paid for, because a request body
  deserialised into memory is the situation this package was written for.
