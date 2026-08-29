using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using PetToys.DbAssistant.Mssql.Extensions;
using PetToys.DbAssistant.Mssql.Test.Accessors;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Bulk;

/// <summary>
/// The defaults of <see cref="SqlBulkOptions"/>, asserted by value.
/// </summary>
/// <remarks>
/// Nothing covered these until now, which is how <see cref="SqlBulkOptions.EnableStreaming"/> came
/// to sit at the opposite of <see cref="SqlBulkCopy.EnableStreaming"/> for as long as it did without
/// anything noticing. A default is part of the public behaviour of a type: changing one changes what
/// every unconfigured caller gets, silently and without a compile error, so each is pinned here.
/// </remarks>
public sealed class SqlBulkOptionsTest
{
    [Fact]
    public void CopyOptions_Unconfigured_MatchesAnUnconfiguredSqlBulkCopy()
    {
        new SqlBulkOptions().CopyOptions.Should().Be(SqlBulkCopyOptions.Default);
    }

    [Fact]
    public void EnableStreaming_Unconfigured_MatchesSqlBulkCopy()
    {
        new SqlBulkOptions().EnableStreaming.Should().BeFalse();
    }

    [Fact]
    public void BulkCopyTimeout_Unconfigured_IsUnbounded()
    {
        // Deliberately not SqlBulkCopy's thirty seconds. A copy this library is written for runs for
        // minutes, and the provider's default would abandon it part-written.
        new SqlBulkOptions().BulkCopyTimeout.Should().Be(0);
    }

    /// <summary>
    /// The instance the library builds for a caller who configures nothing carries the same defaults
    /// as one built here.
    /// </summary>
    /// <remarks>
    /// Asserting <c>new SqlBulkOptions()</c> proves the initialiser and no more. This reaches the
    /// instance the copy path constructs for itself, which is the one whose values are assigned to
    /// the <see cref="SqlBulkCopy"/>. The copy then fails on the connection, which is expected and
    /// is what keeps this test off Docker: the options are built before the connection is touched.
    /// </remarks>
    [Fact]
    public async Task WriteDataAsync_NoOptionsBuilder_UsesTheDeclaredDefaults()
    {
        await using var connection = new SqlConnection();
        SqlBulkOptions? captured = null;

        var act = async () => await connection.CreateBulkContext<NullableEnabledEntity>("table")
            .MapProperty(entity => entity.Int0)
            .WriteDataAsync(Array.Empty<NullableEnabledEntity>(), options => captured = options);

        await act.Should().ThrowAsync<InvalidOperationException>();

        captured.Should().NotBeNull();
        captured!.EnableStreaming.Should().BeFalse();
        captured.CopyOptions.Should().Be(SqlBulkCopyOptions.Default);
        captured.BulkCopyTimeout.Should().Be(0);
    }
}
