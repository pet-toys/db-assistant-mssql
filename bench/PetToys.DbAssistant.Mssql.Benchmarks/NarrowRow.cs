using System;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>A four-column row: the shape most bulk loads actually have.</summary>
public sealed class NarrowRow
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required bool Active { get; init; }
}
