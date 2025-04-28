#nullable disable
using System;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

public sealed class NullableDisabledEntity
{
    public int Int0 { get; init; }

    public int? Int1 { get; init; }

    public DateTime Date0 { get; init; } = DateTime.MinValue;

    public DateTime? Date1 { get; init; }

    public string Str0 { get; init; } = string.Empty;

    public string Str1 { get; init; }

    public byte[] Arr0 { get; init; } = [];

    public byte[] Arr1 { get; init; }
}
