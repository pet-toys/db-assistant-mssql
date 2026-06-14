using AwesomeAssertions;
using PetToys.DbAssistant.Mssql.Extensions;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Extensions;

public sealed class StringExtensionsTest
{
    [Theory]
    [InlineData("abc", "[abc]")]
    [InlineData("[abc", "[[abc]")]
    [InlineData("abc]", "[abc]]]")]
    [InlineData("Weird]Name", "[Weird]]Name]")]
    [InlineData("[abc]", "[abc]")]
    [InlineData("", "")]
    public void QuoteName_Works_Correctly(string value, string expected)
    {
        // Arrange
        // Act
        var result = value.QuoteName();
        // Assert
        result.Should().Be(expected);
    }
}
