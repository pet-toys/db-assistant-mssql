namespace PetToys.DbAssistant.Mssql.Extensions;

internal static class StringExtensions
{
    private const char QuoteStartChar = '[';
    private const char QuoteEndChar = ']';

    public static string QuoteName(this string value) => value switch
    {
        { Length: 0 } => value,
        _ when value.StartsWith(QuoteStartChar) && value.EndsWith(QuoteEndChar) => value,
        _ => QuoteStartChar + value.Replace($"{QuoteEndChar}", $"{QuoteEndChar}{QuoteEndChar}") + QuoteEndChar,
    };
}
