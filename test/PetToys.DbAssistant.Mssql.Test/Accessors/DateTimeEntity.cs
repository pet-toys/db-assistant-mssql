using System;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

/// <summary>
/// The date and time family, each type in both its plain and nullable form.
/// </summary>
/// <remarks>
/// Deliberately a fixture of its own rather than four more properties on
/// <see cref="NullableEnabledEntity"/>. That one is the README's worked example, the shape the
/// Bogus faker generates under <c>StrictMode(true)</c>, and the shape of the table the integration
/// tests create; widening it would drag all three along for no benefit, and none of them is what
/// these types need covering for.
/// </remarks>
public sealed class DateTimeEntity
{
    public int Id { get; init; }

    public DateTimeOffset Offset0 { get; init; }

    public DateTimeOffset? Offset1 { get; init; }

    public TimeSpan Span0 { get; init; }

    public TimeSpan? Span1 { get; init; }

    public DateOnly Date0 { get; init; }

    public DateOnly? Date1 { get; init; }

    public TimeOnly Time0 { get; init; }

    public TimeOnly? Time1 { get; init; }
}
