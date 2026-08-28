using System;
using System.Collections.Generic;
using System.Globalization;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The rows every benchmark copies.
/// </summary>
/// <remarks>
/// Everything here is drawn from one <see cref="Random"/> constructed with a constant seed, and
/// nothing reads the clock, the environment or a new <see cref="Guid"/>. Two runs of the same
/// revision therefore copy identical bytes, which is the only reason their durations can be compared
/// at all. The generators are called from <c>[GlobalSetup]</c>, so no part of building a row lands
/// inside a measured region.
/// </remarks>
public static class RowSet
{
    private const int Seed = 20260828;

    /// <summary>
    /// The threshold at which SQL Server moves a MAX value out of the row and onto the large-object
    /// path. A <c>varchar(max)</c> value at or under it is stored in-row like any other column.
    /// </summary>
    public const int InRowThreshold = 8000;

    /// <summary>
    /// The instant every timestamp is an offset from. A literal, not <see cref="DateTime.UtcNow"/>:
    /// a run's own start time would be one more thing that differs between two runs.
    /// </summary>
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] Names = BuildNames();

    /// <summary>Builds the narrow row set.</summary>
    /// <param name="count">How many rows to build.</param>
    public static IReadOnlyList<NarrowRow> Narrow(int count)
    {
        var random = new Random(Seed);
        var rows = new List<NarrowRow>(count);

        for (var index = 0; index < count; index++)
        {
            rows.Add(new NarrowRow
            {
                Id = index,
                Name = Names[random.Next(Names.Length)],
                CreatedAt = Epoch.AddSeconds(random.Next(0, 31_536_000)),
                Active = random.Next(2) == 0,
            });
        }

        return rows;
    }

    /// <summary>Builds the wide row set.</summary>
    /// <param name="count">How many rows to build.</param>
    public static IReadOnlyList<WideRow> Wide(int count)
    {
        var random = new Random(Seed);
        var payloads = BuildPayloads(random);
        var identifiers = BuildIdentifiers(random);
        var documents = BuildDocuments(random);
        var rows = new List<WideRow>(count);

        for (var index = 0; index < count; index++)
        {
            rows.Add(new WideRow
            {
                Id = index,
                BigId = ((long)index * 2_147_483_647L) + 1,
                Small = (short)random.Next(short.MinValue, short.MaxValue),
                Tiny = (byte)random.Next(byte.MaxValue + 1),
                Code = index.ToString("D8", CultureInfo.InvariantCulture),
                Name = Names[random.Next(Names.Length)],
                Initial = (char)('A' + random.Next(26)),
                Amount = Math.Round((decimal)random.NextDouble() * 10_000m, 2),
                Ratio = random.NextDouble(),
                Factor = (float)random.NextDouble(),
                Flag = random.Next(2) == 0,
                Identifier = identifiers[random.Next(identifiers.Length)],
                Payload = payloads[random.Next(payloads.Length)],
                CreatedAt = Epoch.AddSeconds(random.Next(0, 31_536_000)),
                Document = documents[random.Next(documents.Length)],
            });
        }

        return rows;
    }

    /// <summary>Builds the large-object row set.</summary>
    /// <param name="count">How many rows to build.</param>
    /// <remarks>
    /// Every document is comfortably past <see cref="InRowThreshold"/>, which is the whole point of
    /// the shape: below it the value never takes the path <c>EnableStreaming</c> changes, and the
    /// class would compare a flag against itself.
    /// </remarks>
    public static IReadOnlyList<LargeRow> Large(int count)
    {
        var random = new Random(Seed);
        var documents = BuildLargeDocuments(random);
        var rows = new List<LargeRow>(count);

        for (var index = 0; index < count; index++)
        {
            rows.Add(new LargeRow
            {
                Id = index,
                Document = documents[random.Next(documents.Length)],
            });
        }

        return rows;
    }

    /// <summary>
    /// A pool of off-row documents. Pooled for the same reason the payloads are: at these sizes a
    /// string per row would be most of the set's memory, and what the copy writes is the characters.
    /// </summary>
    private static string[] BuildLargeDocuments(Random random)
    {
        var documents = new string[4];

        for (var index = 0; index < documents.Length; index++)
        {
            var length = InRowThreshold + 512 + (index * 1024);
            var characters = new char[length];

            for (var position = 0; position < length; position++)
            {
                characters[position] = (char)('a' + random.Next(26));
            }

            documents[index] = new string(characters);
        }

        return documents;
    }

    /// <summary>
    /// A pool of names of differing lengths. Text is written by length, so a single fixed string
    /// would measure one length and call it the text column.
    /// </summary>
    private static string[] BuildNames()
    {
        var names = new string[16];

        for (var index = 0; index < names.Length; index++)
        {
            names[index] = string.Create(
                CultureInfo.InvariantCulture,
                $"row-{index:D2}-{new string((char)('a' + index), 4 + (index * 2))}");
        }

        return names;
    }

    /// <summary>
    /// A pool of payloads rather than one array per row: at a hundred thousand rows the per-row
    /// arrays would be most of the set's memory, and what the copy writes is the bytes, not the
    /// reference.
    /// </summary>
    private static byte[][] BuildPayloads(Random random)
    {
        var payloads = new byte[8][];

        for (var index = 0; index < payloads.Length; index++)
        {
            var payload = new byte[32 + (index * 16)];
            random.NextBytes(payload);
            payloads[index] = payload;
        }

        return payloads;
    }

    /// <summary>
    /// A pool of MAX-column values, all comfortably under the 8000-byte threshold at which SQL
    /// Server moves a MAX value off-row. Keeping them in-row is what stops one column from
    /// dominating a wide row's timing; measuring the off-row path is <c>OffRowStreamingBenchmarks</c>'
    /// job, and it uses values of its own.
    /// </summary>
    private static string[] BuildDocuments(Random random)
    {
        var documents = new string[8];

        for (var index = 0; index < documents.Length; index++)
        {
            var length = 192 + (index * 96);
            var characters = new char[length];

            for (var position = 0; position < length; position++)
            {
                characters[position] = (char)('a' + random.Next(26));
            }

            documents[index] = new string(characters);
        }

        return documents;
    }

    private static Guid[] BuildIdentifiers(Random random)
    {
        var identifiers = new Guid[16];
        var bytes = new byte[16];

        for (var index = 0; index < identifiers.Length; index++)
        {
            random.NextBytes(bytes);
            identifiers[index] = new Guid(bytes);
        }

        return identifiers;
    }
}
