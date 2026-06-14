using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PetToys.DbAssistant.Mssql.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Accessors;

/// <summary>
/// Pure unit coverage for the hand-written <c>DbDataReader</c>
/// implementation, exercised without a database. Columns under test:
/// <c>0:Int0</c> (int, not null), <c>1:Str1</c> (string, nullable),
/// <c>2:Arr0</c> (byte[], not null), <c>3:Date0</c> (DateTime, not null).
/// </summary>
public sealed class EntityAccessorTest
{
    private static readonly DateTime SampleDate = new(2026, 6, 14, 1, 2, 3, DateTimeKind.Unspecified);

    private static NullableEnabledEntity FullRow => new()
    {
        Int0 = 7,
        Str1 = "hello",
        Arr0 = [1, 2, 3, 4],
        Date0 = SampleDate,
    };

    private static NullableEnabledEntity NullStringRow => new()
    {
        Int0 = 8,
        Str1 = null,
        Arr0 = [9],
        Date0 = SampleDate,
    };

    [Fact]
    public void Metadata_ReflectsAccessors()
    {
        using var reader = CreateReader();

        reader.FieldCount.Should().Be(4);
        reader.Depth.Should().Be(0);
        reader.RecordsAffected.Should().Be(0);

        reader.GetName(0).Should().Be("Int0");
        reader.GetName(2).Should().Be("Arr0");
        reader.GetOrdinal("Str1").Should().Be(1);
        reader.GetOrdinal("Date0").Should().Be(3);

        reader.GetFieldType(0).Should().Be<int>();
        reader.GetFieldType(2).Should().Be<byte[]>();
        reader.GetDataTypeName(0).Should().Be("Int32");
        reader.GetDataTypeName(3).Should().Be("DateTime");
    }

    [Fact]
    public void Read_IteratesEveryRowThenStops()
    {
        using var reader = CreateReader(FullRow, NullStringRow);

        reader.Read().Should().BeTrue();
        reader.Read().Should().BeTrue();
        reader.Read().Should().BeFalse();
        reader.Read().Should().BeFalse();
    }

    [Fact]
    public void Indexers_And_TypedGetters_ReturnValues()
    {
        using var reader = CreateReader(FullRow);
        reader.Read().Should().BeTrue();

        reader[0].Should().Be(7);
        reader["Str1"].Should().Be("hello");
        reader.GetValue(0).Should().Be(7);
        reader["Arr0"].Should().BeOfType<byte[]>().Which.Should().Equal(new byte[] { 1, 2, 3, 4 });

        reader.GetInt32(0).Should().Be(7);
        reader.GetString(1).Should().Be("hello");
        reader.GetDateTime(3).Should().Be(SampleDate);
    }

    [Fact]
    public void NullReferenceValue_MapsToDbNull()
    {
        using var reader = CreateReader(NullStringRow);
        reader.Read().Should().BeTrue();

        reader[1].Should().Be(DBNull.Value);
        reader["Str1"].Should().Be(DBNull.Value);
        reader.IsDBNull(1).Should().BeTrue();
        reader.IsDBNull(0).Should().BeFalse();
    }

    [Fact]
    public void GetValues_FillsArrayUpToFieldCount()
    {
        using var reader = CreateReader(FullRow);
        reader.Read().Should().BeTrue();

        var values = new object[4];
        reader.GetValues(values).Should().Be(4);
        values[0].Should().Be(7);
        values[1].Should().Be("hello");
        values[3].Should().Be(SampleDate);
    }

    [Theory]
    [InlineData(0, 4, new byte[] { 1, 2, 3, 4 }, 4)] // full copy
    [InlineData(2, 4, new byte[] { 3, 4 }, 2)]       // offset past the start, clamped to remaining
    [InlineData(0, 2, new byte[] { 1, 2 }, 2)]       // length shorter than the source
    [InlineData(10, 4, new byte[] { }, 0)]           // offset beyond the source
    public void GetBytes_CopiesRequestedWindow(long dataOffset, int length, byte[] expected, long expectedCount)
    {
        using var reader = CreateReader(FullRow);
        reader.Read().Should().BeTrue();

        var buffer = new byte[length];
        var copied = reader.GetBytes(2, dataOffset, buffer, 0, length);

        copied.Should().Be(expectedCount);
        buffer.Take((int)expectedCount).Should().Equal(expected);
    }

    [Theory]
    [InlineData(0, 5, "hello", 5)]
    [InlineData(3, 5, "lo", 2)]
    [InlineData(10, 5, "", 0)]
    public void GetChars_CopiesRequestedWindow(long dataOffset, int length, string expected, long expectedCount)
    {
        using var reader = CreateReader(FullRow);
        reader.Read().Should().BeTrue();

        var buffer = new char[length];
        var copied = reader.GetChars(1, dataOffset, buffer, 0, length);

        copied.Should().Be(expectedCount);
        new string(buffer, 0, (int)expectedCount).Should().Be(expected);
    }

    [Fact]
    public void GetSchemaTable_DescribesColumns()
    {
        using var reader = CreateReader();

        var schema = reader.GetSchemaTable();

        schema.Rows.Count.Should().Be(4);
        schema.Rows[0]["ColumnName"].Should().Be("[Int0]");
        schema.Rows[0]["DataType"].Should().Be(typeof(int));
        schema.Rows[0]["AllowDBNull"].Should().Be(false);
        schema.Rows[1]["ColumnName"].Should().Be("[Str1]");
        schema.Rows[1]["AllowDBNull"].Should().Be(true);
    }

    [Fact]
    public void NextResult_ReturnsFalseAndEndsReading()
    {
        using var reader = CreateReader(FullRow);

        reader.HasRows.Should().BeTrue();
        reader.NextResult().Should().BeFalse();
        reader.HasRows.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ClosesTheReader()
    {
        var reader = CreateReader(FullRow);

        reader.IsClosed.Should().BeFalse();
        reader.Dispose();
        reader.IsClosed.Should().BeTrue();
    }

    private static EntityAccessor<NullableEnabledEntity> CreateReader(params NullableEnabledEntity[] rows)
    {
        var accessors = new List<IPropertyAccessor<NullableEnabledEntity>>
        {
            new PropertyAccessor<NullableEnabledEntity, int>(e => e.Int0),
            new PropertyAccessor<NullableEnabledEntity, string?>(e => e.Str1),
            new PropertyAccessor<NullableEnabledEntity, byte[]>(e => e.Arr0),
            new PropertyAccessor<NullableEnabledEntity, DateTime>(e => e.Date0),
        };

        return new EntityAccessor<NullableEnabledEntity>(rows, accessors);
    }
}
