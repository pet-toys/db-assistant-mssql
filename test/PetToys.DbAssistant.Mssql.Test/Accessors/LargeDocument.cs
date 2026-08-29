namespace PetToys.DbAssistant.Mssql.Test.Accessors;

/// <summary>
/// A row whose single value is large enough that SQL Server stores it off-row, which is the shape
/// <c>SqlBulkOptions.EnableStreaming</c> exists for.
/// </summary>
public sealed class LargeDocument
{
    public int Id { get; init; }

    public string Document { get; init; } = string.Empty;
}
