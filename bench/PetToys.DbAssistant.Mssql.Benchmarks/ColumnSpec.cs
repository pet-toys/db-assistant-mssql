using System;

namespace PetToys.DbAssistant.Mssql.Benchmarks;

/// <summary>
/// One destination column: what it is called, what SQL Server type it holds, and the CLR type the
/// row exposes it as.
/// </summary>
/// <remarks>
/// A benchmark class declares its columns once, in the order every arm writes them, and the
/// destination table, the <c>DataTable</c> arm's column collection, both readers' ordinals and the
/// mapped context's column mappings are all derived from that declaration. Spelling them out four
/// times is how a reordered column ends up written into another column of the same type with every
/// arm still succeeding and no two of them measuring the same work.
/// <para>
/// <see cref="Name"/> is the property name as well as the column name. Keeping them equal is what
/// lets the reflective arm resolve a column to a property without a second mapping table, and it is
/// also the library's own default, so the mapped arm needs no column name argument.
/// </para>
/// </remarks>
/// <param name="Name">The column name, which is also the row property's name.</param>
/// <param name="DataType">The column's SQL Server type, as it appears in <c>CREATE TABLE</c>.</param>
/// <param name="ClrType">The non-nullable CLR type the column is written from.</param>
public sealed record ColumnSpec(string Name, string DataType, Type ClrType);
