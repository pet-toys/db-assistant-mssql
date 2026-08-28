using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Extensions;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The wide row's columns and its mapping, declared once because two classes copy this shape.
/// </summary>
/// <remarks>
/// <see cref="WideRowBenchmarks"/> compares the four ways of getting the shape to the server;
/// <see cref="InRowStreamingBenchmarks"/> compares one flag over it. Their numbers are only
/// readable beside each other while both copy the same fifteen columns in the same order, and a
/// column added to one declaration and not the other is exactly the kind of divergence that would
/// move every figure in both reports without appearing in either.
/// </remarks>
internal static class WideRowMapping
{
    /// <summary>The destination columns, in the order every arm writes them.</summary>
    public static IReadOnlyList<ColumnSpec> Columns { get; } =
    [
        new("Id", "int", typeof(int)),
        new("BigId", "bigint", typeof(long)),
        new("Small", "smallint", typeof(short)),
        new("Tiny", "tinyint", typeof(byte)),
        new("Code", "varchar(16)", typeof(string)),
        new("Name", "nvarchar(64)", typeof(string)),
        new("Initial", "nchar(1)", typeof(char)),
        new("Amount", "decimal(18,2)", typeof(decimal)),
        new("Ratio", "float", typeof(double)),
        new("Factor", "real", typeof(float)),
        new("Flag", "bit", typeof(bool)),
        new("Identifier", "uniqueidentifier", typeof(Guid)),
        new("Payload", "varbinary(256)", typeof(byte[])),
        new("CreatedAt", "datetime2", typeof(DateTime)),
        new("Document", "varchar(max)", typeof(string)),
    ];

    /// <summary>Maps every column of the wide row onto a destination table.</summary>
    /// <param name="connection">The open connection to copy through.</param>
    /// <param name="destination">The unquoted name of the destination table.</param>
    /// <returns>The builder, mapped and ready to be written through.</returns>
    public static BulkContextBuilder<WideRow> Map(SqlConnection connection, string destination) =>
        connection.CreateBulkContext<WideRow>(destination)
            .MapProperty(row => row.Id)
            .MapProperty(row => row.BigId)
            .MapProperty(row => row.Small)
            .MapProperty(row => row.Tiny)
            .MapProperty(row => row.Code)
            .MapProperty(row => row.Name)
            .MapProperty(row => row.Initial)
            .MapProperty(row => row.Amount)
            .MapProperty(row => row.Ratio)
            .MapProperty(row => row.Factor)
            .MapProperty(row => row.Flag)
            .MapProperty(row => row.Identifier)
            .MapProperty(row => row.Payload)
            .MapProperty(row => row.CreatedAt)
            .MapProperty(row => row.Document);
}
