# Baseline

The recorded run every later measurement is compared against. It is a copy of the
`*-report-github.md` exports, taken with the benchmark project's own job on the
environment below.

**Ratios travel between machines; durations do not.** `Mean` is only comparable
within the environment it was recorded in, and here that includes the server: a
different image, host, disk, or a connection over a network rather than a
loopback moves every number without anything in the library changing. Compare
`Ratio` and `Alloc Ratio`.

See [README.md](README.md) for what each arm is, why the `DataTable` is the
baseline, and what the durations do and do not include.

## Environment

The server: a `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` container
started by the runner, its database in SIMPLE recovery. Every destination is a
heap with no index or constraint and every arm copies with
`SqlBulkCopyOptions.TableLock`, which together are what SQL Server needs to take
the minimally-logged path. That falls on every arm equally, so the ratios are
unaffected, but a duration here is a floor rather than what a copy into a real,
indexed, fully-logged table costs.

The host:

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]         : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Copy .NET 10.0 : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Copy .NET 8.0  : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3
  Copy .NET 9.0  : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=15  RunStrategy=Monitoring  
UnrollFactor=1  WarmupCount=5
```

## Narrow row

Four columns, four arms.

| Method                | Job            | Runtime   | RowCount | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated | Alloc Ratio |
|---------------------- |--------------- |---------- |--------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|------------:|
| **DataTable**             | **Copy .NET 10.0** | **.NET 10.0** | **10000**    |  **25.95 ms** |  **4.249 ms** |  **3.975 ms** |  **1.02** |    **0.20** |         **-** |         **-** |   **3.99 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 10.0 | .NET 10.0 | 10000    |  49.11 ms |  8.260 ms |  7.727 ms |  1.93 |    0.38 |         - |         - |    5.9 MB |        1.48 |
| &#39;Hand-written reader&#39; | Copy .NET 10.0 | .NET 10.0 | 10000    |  48.58 ms |  9.065 ms |  8.480 ms |  1.91 |    0.40 |         - |         - |   5.89 MB |        1.48 |
| &#39;Mapped bulk context&#39; | Copy .NET 10.0 | .NET 10.0 | 10000    |  47.06 ms | 12.524 ms | 11.715 ms |  1.85 |    0.50 |         - |         - |   5.93 MB |        1.49 |
|                       |                |           |          |           |           |           |       |         |           |           |           |             |
| **DataTable**             | **Copy .NET 10.0** | **.NET 10.0** | **100000**   | **139.98 ms** | **22.958 ms** | **21.475 ms** |  **1.02** |    **0.19** | **3000.0000** | **1000.0000** |  **39.49 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 10.0 | .NET 10.0 | 100000   | 207.04 ms | 20.164 ms | 18.861 ms |  1.50 |    0.22 | 4000.0000 |         - |  58.73 MB |        1.49 |
| &#39;Hand-written reader&#39; | Copy .NET 10.0 | .NET 10.0 | 100000   | 216.09 ms |  8.906 ms |  8.331 ms |  1.57 |    0.19 | 4000.0000 |         - |  58.72 MB |        1.49 |
| &#39;Mapped bulk context&#39; | Copy .NET 10.0 | .NET 10.0 | 100000   | 222.71 ms |  8.921 ms |  8.344 ms |  1.62 |    0.19 | 4000.0000 |         - |  58.75 MB |        1.49 |
|                       |                |           |          |           |           |           |       |         |           |           |           |             |
| **DataTable**             | **Copy .NET 8.0**  | **.NET 8.0**  | **10000**    |  **25.34 ms** |  **2.484 ms** |  **2.323 ms** |  **1.01** |    **0.12** |         **-** |         **-** |   **3.99 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 8.0  | .NET 8.0  | 10000    |  50.64 ms |  8.125 ms |  7.600 ms |  2.01 |    0.34 |         - |         - |    5.9 MB |        1.48 |
| &#39;Hand-written reader&#39; | Copy .NET 8.0  | .NET 8.0  | 10000    |  41.77 ms |  6.887 ms |  6.442 ms |  1.66 |    0.28 |         - |         - |   5.89 MB |        1.48 |
| &#39;Mapped bulk context&#39; | Copy .NET 8.0  | .NET 8.0  | 10000    |  48.76 ms | 10.554 ms |  9.872 ms |  1.94 |    0.41 |         - |         - |   5.93 MB |        1.49 |
|                       |                |           |          |           |           |           |       |         |           |           |           |             |
| **DataTable**             | **Copy .NET 8.0**  | **.NET 8.0**  | **100000**   | **140.45 ms** |  **7.790 ms** |  **7.287 ms** |  **1.00** |    **0.07** | **3000.0000** | **1000.0000** |  **39.49 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 8.0  | .NET 8.0  | 100000   | 228.14 ms | 18.195 ms | 17.020 ms |  1.63 |    0.14 | 4000.0000 |         - |  58.73 MB |        1.49 |
| &#39;Hand-written reader&#39; | Copy .NET 8.0  | .NET 8.0  | 100000   | 246.37 ms | 14.101 ms | 13.190 ms |  1.76 |    0.13 | 4000.0000 |         - |  58.72 MB |        1.49 |
| &#39;Mapped bulk context&#39; | Copy .NET 8.0  | .NET 8.0  | 100000   | 233.84 ms | 11.045 ms | 10.332 ms |  1.67 |    0.11 | 4000.0000 |         - |  58.76 MB |        1.49 |
|                       |                |           |          |           |           |           |       |         |           |           |           |             |
| **DataTable**             | **Copy .NET 9.0**  | **.NET 9.0**  | **10000**    |  **30.55 ms** |  **5.243 ms** |  **4.904 ms** |  **1.03** |    **0.23** |         **-** |         **-** |   **3.99 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 9.0  | .NET 9.0  | 10000    |  45.18 ms |  8.009 ms |  7.492 ms |  1.52 |    0.35 |         - |         - |    5.9 MB |        1.48 |
| &#39;Hand-written reader&#39; | Copy .NET 9.0  | .NET 9.0  | 10000    |  45.18 ms |  9.568 ms |  8.950 ms |  1.52 |    0.38 |         - |         - |   5.89 MB |        1.48 |
| &#39;Mapped bulk context&#39; | Copy .NET 9.0  | .NET 9.0  | 10000    |  46.72 ms | 13.412 ms | 12.546 ms |  1.57 |    0.49 |         - |         - |   5.93 MB |        1.49 |
|                       |                |           |          |           |           |           |       |         |           |           |           |             |
| **DataTable**             | **Copy .NET 9.0**  | **.NET 9.0**  | **100000**   | **140.56 ms** | **17.135 ms** | **16.028 ms** |  **1.01** |    **0.14** | **3000.0000** | **1000.0000** |  **39.49 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 9.0  | .NET 9.0  | 100000   | 220.15 ms | 16.470 ms | 15.406 ms |  1.58 |    0.18 | 4000.0000 |         - |  58.73 MB |        1.49 |
| &#39;Hand-written reader&#39; | Copy .NET 9.0  | .NET 9.0  | 100000   | 220.20 ms |  9.867 ms |  9.230 ms |  1.58 |    0.15 | 4000.0000 |         - |   58.7 MB |        1.49 |
| &#39;Mapped bulk context&#39; | Copy .NET 9.0  | .NET 9.0  | 100000   | 226.54 ms |  6.393 ms |  5.980 ms |  1.63 |    0.15 | 4000.0000 |         - |  58.74 MB |        1.49 |

## Wide row

Fifteen columns, the same four arms.

| Method                | Job            | Runtime   | RowCount | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0       | Gen1      | Gen2      | Allocated | Alloc Ratio |
|---------------------- |--------------- |---------- |--------- |------------:|-----------:|-----------:|------:|--------:|-----------:|----------:|----------:|----------:|------------:|
| **DataTable**             | **Copy .NET 10.0** | **.NET 10.0** | **10000**    |   **130.22 ms** |  **58.907 ms** |  **55.101 ms** |  **1.16** |    **0.66** |  **1000.0000** |         **-** |         **-** |   **12.3 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 10.0 | .NET 10.0 | 10000    |   145.33 ms |  74.123 ms |  69.334 ms |  1.30 |    0.80 |          - |         - |         - |   8.92 MB |        0.73 |
| &#39;Hand-written reader&#39; | Copy .NET 10.0 | .NET 10.0 | 10000    |   106.00 ms |  11.441 ms |  10.702 ms |  0.95 |    0.36 |          - |         - |         - |   8.89 MB |        0.72 |
| &#39;Mapped bulk context&#39; | Copy .NET 10.0 | .NET 10.0 | 10000    |   131.21 ms |  46.031 ms |  43.057 ms |  1.17 |    0.58 |          - |         - |         - |   9.01 MB |        0.73 |
|                       |                |           |          |             |            |            |       |         |            |           |           |           |             |
| **DataTable**             | **Copy .NET 10.0** | **.NET 10.0** | **100000**   |   **702.18 ms** |  **11.325 ms** |  **10.593 ms** |  **1.00** |    **0.02** | **10000.0000** | **4000.0000** | **1000.0000** | **122.45 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 10.0 | .NET 10.0 | 100000   |   983.40 ms |  17.644 ms |  16.505 ms |  1.40 |    0.03 |  7000.0000 |         - |         - |  88.79 MB |        0.73 |
| &#39;Hand-written reader&#39; | Copy .NET 10.0 | .NET 10.0 | 100000   |   888.95 ms |  22.874 ms |  21.396 ms |  1.27 |    0.03 |  7000.0000 |         - |         - |  88.77 MB |        0.72 |
| &#39;Mapped bulk context&#39; | Copy .NET 10.0 | .NET 10.0 | 100000   |   910.14 ms |  56.872 ms |  53.198 ms |  1.30 |    0.08 |  7000.0000 |         - |         - |  88.87 MB |        0.73 |
|                       |                |           |          |             |            |            |       |         |            |           |           |           |             |
| **DataTable**             | **Copy .NET 8.0**  | **.NET 8.0**  | **10000**    |    **84.22 ms** |  **10.467 ms** |   **9.791 ms** |  **1.01** |    **0.16** |  **1000.0000** |         **-** |         **-** |  **12.29 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 8.0  | .NET 8.0  | 10000    |   126.27 ms |  49.369 ms |  46.180 ms |  1.52 |    0.56 |          - |         - |         - |   8.92 MB |        0.73 |
| &#39;Hand-written reader&#39; | Copy .NET 8.0  | .NET 8.0  | 10000    |   111.19 ms |   5.688 ms |   5.320 ms |  1.34 |    0.16 |          - |         - |         - |   8.89 MB |        0.72 |
| &#39;Mapped bulk context&#39; | Copy .NET 8.0  | .NET 8.0  | 10000    |   118.47 ms |   6.458 ms |   6.041 ms |  1.42 |    0.17 |          - |         - |         - |      9 MB |        0.73 |
|                       |                |           |          |             |            |            |       |         |            |           |           |           |             |
| **DataTable**             | **Copy .NET 8.0**  | **.NET 8.0**  | **100000**   |   **726.75 ms** |  **17.984 ms** |  **16.822 ms** |  **1.00** |    **0.03** | **10000.0000** | **4000.0000** | **1000.0000** | **122.43 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 8.0  | .NET 8.0  | 100000   | 1,017.07 ms |  24.666 ms |  23.072 ms |  1.40 |    0.04 |  7000.0000 |         - |         - |   88.8 MB |        0.73 |
| &#39;Hand-written reader&#39; | Copy .NET 8.0  | .NET 8.0  | 100000   |   937.37 ms |  61.366 ms |  57.402 ms |  1.29 |    0.08 |  7000.0000 |         - |         - |  88.76 MB |        0.72 |
| &#39;Mapped bulk context&#39; | Copy .NET 8.0  | .NET 8.0  | 100000   |   980.36 ms |  35.705 ms |  33.399 ms |  1.35 |    0.05 |  7000.0000 |         - |         - |  88.86 MB |        0.73 |
|                       |                |           |          |             |            |            |       |         |            |           |           |           |             |
| **DataTable**             | **Copy .NET 9.0**  | **.NET 9.0**  | **10000**    |    **83.47 ms** |  **12.298 ms** |  **11.504 ms** |  **1.02** |    **0.18** |  **1000.0000** |         **-** |         **-** |  **12.32 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 9.0  | .NET 9.0  | 10000    |   112.05 ms |   4.408 ms |   4.124 ms |  1.36 |    0.17 |          - |         - |         - |   8.93 MB |        0.72 |
| &#39;Hand-written reader&#39; | Copy .NET 9.0  | .NET 9.0  | 10000    |   110.54 ms |   9.125 ms |   8.535 ms |  1.35 |    0.19 |          - |         - |         - |   8.89 MB |        0.72 |
| &#39;Mapped bulk context&#39; | Copy .NET 9.0  | .NET 9.0  | 10000    |   112.73 ms |  10.133 ms |   9.479 ms |  1.37 |    0.20 |          - |         - |         - |   8.99 MB |        0.73 |
|                       |                |           |          |             |            |            |       |         |            |           |           |           |             |
| **DataTable**             | **Copy .NET 9.0**  | **.NET 9.0**  | **100000**   |   **707.09 ms** |   **7.550 ms** |   **7.062 ms** |  **1.00** |    **0.01** | **10000.0000** | **4000.0000** | **1000.0000** | **122.46 MB** |        **1.00** |
| &#39;Reflective reader&#39;   | Copy .NET 9.0  | .NET 9.0  | 100000   |   986.21 ms | 145.249 ms | 135.866 ms |  1.39 |    0.19 |  7000.0000 |         - |         - |  88.78 MB |        0.72 |
| &#39;Hand-written reader&#39; | Copy .NET 9.0  | .NET 9.0  | 100000   | 1,089.04 ms |  86.133 ms |  80.569 ms |  1.54 |    0.11 |  7000.0000 |         - |         - |  88.75 MB |        0.72 |
| &#39;Mapped bulk context&#39; | Copy .NET 9.0  | .NET 9.0  | 100000   |   940.28 ms |  26.628 ms |  24.908 ms |  1.33 |    0.04 |  7000.0000 |         - |         - |  88.85 MB |        0.73 |

## Source shape

`IEnumerable` against `IAsyncEnumerable`, over the wide row.

| Method                    | Job            | Runtime   | RowCount | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated | Alloc Ratio |
|-------------------------- |--------------- |---------- |--------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|------------:|
| **&#39;IEnumerable source&#39;**      | **Copy .NET 10.0** | **.NET 10.0** | **10000**    |  **49.71 ms** | **12.529 ms** | **11.720 ms** |  **1.05** |    **0.33** |         **-** |   **5.93 MB** |        **1.00** |
| &#39;IAsyncEnumerable source&#39; | Copy .NET 10.0 | .NET 10.0 | 10000    |  53.24 ms | 10.559 ms |  9.877 ms |  1.12 |    0.32 |         - |   5.93 MB |        1.00 |
|                           |                |           |          |           |           |           |       |         |           |           |             |
| **&#39;IEnumerable source&#39;**      | **Copy .NET 10.0** | **.NET 10.0** | **100000**   | **221.67 ms** |  **8.098 ms** |  **7.575 ms** |  **1.00** |    **0.05** | **4000.0000** |  **58.76 MB** |        **1.00** |
| &#39;IAsyncEnumerable source&#39; | Copy .NET 10.0 | .NET 10.0 | 100000   | 223.99 ms |  9.185 ms |  8.592 ms |  1.01 |    0.05 | 4000.0000 |  58.75 MB |        1.00 |
|                           |                |           |          |           |           |           |       |         |           |           |             |
| **&#39;IEnumerable source&#39;**      | **Copy .NET 8.0**  | **.NET 8.0**  | **10000**    |  **50.62 ms** |  **5.764 ms** |  **5.391 ms** |  **1.01** |    **0.14** |         **-** |   **5.93 MB** |        **1.00** |
| &#39;IAsyncEnumerable source&#39; | Copy .NET 8.0  | .NET 8.0  | 10000    |  46.31 ms | 11.646 ms | 10.894 ms |  0.92 |    0.23 |         - |   5.93 MB |        1.00 |
|                           |                |           |          |           |           |           |       |         |           |           |             |
| **&#39;IEnumerable source&#39;**      | **Copy .NET 8.0**  | **.NET 8.0**  | **100000**   | **231.44 ms** |  **8.779 ms** |  **8.212 ms** |  **1.00** |    **0.05** | **4000.0000** |  **58.76 MB** |        **1.00** |
| &#39;IAsyncEnumerable source&#39; | Copy .NET 8.0  | .NET 8.0  | 100000   | 237.87 ms |  8.305 ms |  7.769 ms |  1.03 |    0.05 | 4000.0000 |  58.76 MB |        1.00 |
|                           |                |           |          |           |           |           |       |         |           |           |             |
| **&#39;IEnumerable source&#39;**      | **Copy .NET 9.0**  | **.NET 9.0**  | **10000**    |  **54.48 ms** |  **9.216 ms** |  **8.620 ms** |  **1.03** |    **0.25** |         **-** |   **5.93 MB** |        **1.00** |
| &#39;IAsyncEnumerable source&#39; | Copy .NET 9.0  | .NET 9.0  | 10000    |  48.32 ms | 14.025 ms | 13.119 ms |  0.91 |    0.29 |         - |   5.93 MB |        1.00 |
|                           |                |           |          |           |           |           |       |         |           |           |             |
| **&#39;IEnumerable source&#39;**      | **Copy .NET 9.0**  | **.NET 9.0**  | **100000**   | **227.96 ms** |  **9.475 ms** |  **8.863 ms** |  **1.00** |    **0.05** | **4000.0000** |  **58.74 MB** |        **1.00** |
| &#39;IAsyncEnumerable source&#39; | Copy .NET 9.0  | .NET 9.0  | 100000   | 229.88 ms | 12.338 ms | 11.541 ms |  1.01 |    0.06 | 4000.0000 |  58.76 MB |        1.00 |

## Streaming, off-row values

`EnableStreaming` off against on, over documents SQL Server stores off-row.

| Method                              | Job            | Runtime   | RowCount | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Gen0      | Allocated | Alloc Ratio |
|------------------------------------ |--------------- |---------- |--------- |----------:|---------:|---------:|----------:|------:|--------:|----------:|----------:|------------:|
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 10.0** | **.NET 10.0** | **1000**     |  **95.06 ms** | **22.22 ms** | **20.78 ms** |  **88.60 ms** |  **1.03** |    **0.27** |         **-** |  **10.13 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 10.0 | .NET 10.0 | 1000     | 136.91 ms | 73.67 ms | 68.91 ms | 104.37 ms |  1.48 |    0.76 |         - |   5.97 MB |        0.59 |
|                                     |                |           |          |           |          |          |           |       |         |           |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 10.0** | **.NET 10.0** | **5000**     | **399.06 ms** | **11.94 ms** | **11.17 ms** | **398.45 ms** |  **1.00** |    **0.04** | **4000.0000** |  **50.62 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 10.0 | .NET 10.0 | 5000     | 403.92 ms | 27.29 ms | 25.53 ms | 393.90 ms |  1.01 |    0.07 | 2000.0000 |  29.58 MB |        0.58 |
|                                     |                |           |          |           |          |          |           |       |         |           |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 8.0**  | **.NET 8.0**  | **1000**     |  **84.94 ms** | **11.22 ms** | **10.50 ms** |  **82.81 ms** |  **1.01** |    **0.17** |         **-** |  **10.16 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 8.0  | .NET 8.0  | 1000     | 104.34 ms | 45.68 ms | 42.73 ms |  94.24 ms |  1.25 |    0.52 |         - |   5.95 MB |        0.59 |
|                                     |                |           |          |           |          |          |           |       |         |           |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 8.0**  | **.NET 8.0**  | **5000**     | **374.23 ms** | **14.95 ms** | **13.99 ms** | **369.48 ms** |  **1.00** |    **0.05** | **4000.0000** |   **50.6 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 8.0  | .NET 8.0  | 5000     | 401.52 ms | 21.93 ms | 20.52 ms | 399.19 ms |  1.07 |    0.07 | 2000.0000 |  29.61 MB |        0.59 |
|                                     |                |           |          |           |          |          |           |       |         |           |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 9.0**  | **.NET 9.0**  | **1000**     |  **99.60 ms** | **44.17 ms** | **41.32 ms** |  **88.40 ms** |  **1.08** |    **0.48** |         **-** |  **10.12 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 9.0  | .NET 9.0  | 1000     | 103.94 ms | 45.28 ms | 42.36 ms |  91.74 ms |  1.12 |    0.50 |         - |   5.96 MB |        0.59 |
|                                     |                |           |          |           |          |          |           |       |         |           |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 9.0**  | **.NET 9.0**  | **5000**     | **381.21 ms** | **14.21 ms** | **13.29 ms** | **378.94 ms** |  **1.00** |    **0.05** | **4000.0000** |  **50.58 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 9.0  | .NET 9.0  | 5000     | 399.12 ms | 18.19 ms | 17.01 ms | 395.36 ms |  1.05 |    0.06 | 2000.0000 |  29.61 MB |        0.59 |

## Streaming, in-row values

The same two arms over the wide row, where there is nothing to stream.

| Method                              | Job            | Runtime   | RowCount | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0       | Allocated | Alloc Ratio |
|------------------------------------ |--------------- |---------- |--------- |-----------:|---------:|---------:|------:|--------:|-----------:|----------:|------------:|
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 10.0** | **.NET 10.0** | **10000**    |   **124.5 ms** | **18.72 ms** | **17.51 ms** |  **1.02** |    **0.19** |          **-** |   **8.99 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 10.0 | .NET 10.0 | 10000    |   220.8 ms | 10.54 ms |  9.86 ms |  1.80 |    0.24 |  5000.0000 |  63.36 MB |        7.04 |
|                                     |                |           |          |            |          |          |       |         |            |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 10.0** | **.NET 10.0** | **100000**   | **1,209.0 ms** | **52.67 ms** | **49.27 ms** |  **1.00** |    **0.05** |  **7000.0000** |  **88.86 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 10.0 | .NET 10.0 | 100000   | 1,883.1 ms | 66.93 ms | 62.61 ms |  1.56 |    0.08 | 55000.0000 |    632 MB |        7.11 |
|                                     |                |           |          |            |          |          |       |         |            |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 8.0**  | **.NET 8.0**  | **10000**    |   **135.9 ms** | **12.85 ms** | **12.02 ms** |  **1.01** |    **0.12** |          **-** |      **9 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 8.0  | .NET 8.0  | 10000    |   241.5 ms | 18.35 ms | 17.16 ms |  1.79 |    0.19 |  5000.0000 |  63.35 MB |        7.04 |
|                                     |                |           |          |            |          |          |       |         |            |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 8.0**  | **.NET 8.0**  | **100000**   |   **968.8 ms** | **62.40 ms** | **58.37 ms** |  **1.00** |    **0.08** |  **7000.0000** |  **88.86 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 8.0  | .NET 8.0  | 100000   | 2,023.0 ms | 81.21 ms | 75.97 ms |  2.10 |    0.14 | 55000.0000 | 632.38 MB |        7.12 |
|                                     |                |           |          |            |          |          |       |         |            |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 9.0**  | **.NET 9.0**  | **10000**    |   **128.8 ms** | **14.18 ms** | **13.26 ms** |  **1.01** |    **0.14** |          **-** |   **8.99 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 9.0  | .NET 9.0  | 10000    |   233.6 ms | 10.06 ms |  9.41 ms |  1.83 |    0.19 |  5000.0000 |  63.32 MB |        7.04 |
|                                     |                |           |          |            |          |          |       |         |            |           |             |
| **&#39;EnableStreaming off (the default)&#39;** | **Copy .NET 9.0**  | **.NET 9.0**  | **100000**   |   **915.5 ms** | **44.04 ms** | **41.20 ms** |  **1.00** |    **0.06** |  **7000.0000** |  **88.86 MB** |        **1.00** |
| &#39;EnableStreaming on&#39;                | Copy .NET 9.0  | .NET 9.0  | 100000   | 1,896.0 ms | 22.24 ms | 20.80 ms |  2.07 |    0.09 | 55000.0000 | 632.21 MB |        7.12 |
