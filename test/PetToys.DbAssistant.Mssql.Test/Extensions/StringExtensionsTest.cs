using AwesomeAssertions;
using PetToys.DbAssistant.Mssql.Extensions;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test.Extensions;

public sealed class StringExtensionsTest
{
    [Theory]
    [InlineData("abc", "[abc]")]                  // plain name is wrapped
    [InlineData("[abc", "[[abc]")]                // leading bracket only is not "already quoted"
    [InlineData("abc]", "[abc]]]")]               // trailing closing bracket is escaped
    [InlineData("Weird]Name", "[Weird]]Name]")]   // inner closing bracket is doubled
    [InlineData("a]]b", "[a]]]]b]")]              // each closing bracket is doubled independently
    [InlineData("]", "[]]]")]                      // lone closing bracket
    [InlineData("[abc]", "[abc]")]                // already quoted is returned unchanged
    [InlineData("[]", "[]")]                       // empty quoted identifier is returned unchanged
    [InlineData("  ", "[  ]")]                     // whitespace is wrapped, not trimmed
    [InlineData("", "")]                           // empty string is returned unchanged
    public void QuoteName_QuotesAndEscapes(string value, string expected)
        => value.QuoteName().Should().Be(expected);

    [Fact]
    public void QuoteName_AlreadyBracketedButMalformed_IsLeftUnchanged()
    {
        // Documents the heuristic limitation: a value that merely starts with '['
        // and ends with ']' is treated as already quoted, even when the inner
        // bracket is unbalanced.
        "[Weird]Name]".QuoteName().Should().Be("[Weird]Name]");
    }
}
