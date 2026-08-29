using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Extensions;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// The narrow row's columns and its mapping, declared once because two things copy this shape.
/// </summary>
/// <remarks>
/// <see cref="NarrowRowBenchmarks"/> compares the four ways of getting the shape to the server, and
/// the capacity probe copies the same shape to find where it stops fitting. A probe result is read
/// beside the recorded baseline, so the two have to be copying the same four columns in the same
/// order into the same column types; a divergence here would leave both numbers looking comparable
/// and describing different work.
/// </remarks>
internal static class NarrowRowMapping
{
    /// <summary>The destination columns, in the order every arm writes them.</summary>
    public static IReadOnlyList<ColumnSpec> Columns { get; } =
    [
        new("Id", "int", typeof(int)),
        new("Name", "nvarchar(64)", typeof(string)),
        new("CreatedAt", "datetime2", typeof(DateTime)),
        new("Active", "bit", typeof(bool)),
    ];

    /// <summary>Maps every column of the narrow row onto a destination.</summary>
    /// <param name="connection">The connection to copy through.</param>
    /// <param name="destination">The unquoted destination table name.</param>
    public static BulkContextBuilder<NarrowRow> Map(SqlConnection connection, string destination) =>
        connection.CreateBulkContext<NarrowRow>(destination)
            .MapProperty(row => row.Id)
            .MapProperty(row => row.Name)
            .MapProperty(row => row.CreatedAt)
            .MapProperty(row => row.Active);

    /// <summary>Writes one row's values into a buffer, in column order.</summary>
    /// <param name="values">The buffer to fill; its length is the column count.</param>
    /// <param name="row">The row to read.</param>
    public static void Fill(object[] values, NarrowRow row)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(row);
        values[0] = row.Id;
        values[1] = row.Name;
        values[2] = row.CreatedAt;
        values[3] = row.Active;
    }
}
