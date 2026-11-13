using System.Diagnostics.CodeAnalysis;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>One avansert søk condition, the API's <c>vilkar=felt:operator:verdi</c>. The value is in the API's form,
/// with no line break: text as typed, a day <c>yyyy-MM-dd</c>, a range <c>yyyy-MM-dd..yyyy-MM-dd</c>, codes
/// comma-separated.</summary>
public sealed record SearchCondition(string Field, string Operator, string Value)
{
    /// <summary>The most conditions one search takes, which is all the API accepts.</summary>
    public const int MaxCount = 10;

    /// <summary>The longest text value the API accepts.</summary>
    public const int MaxTextLength = 200;

    /// <summary>The most codes one condition takes, which is all the API accepts.</summary>
    public const int MaxCodes = 20;

    /// <summary>The wire form, <c>felt:operator:verdi</c>.</summary>
    public override string ToString() => $"{Field}:{Operator}:{Value}";

    /// <summary>Read the wire form back; everything after the second colon is the value. False for a blank value or a
    /// field or operator that is not a plain token.</summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out SearchCondition? condition)
    {
        condition = null;
        var parts = text?.Split(':', 3);

        if (parts is not { Length: 3 } || !IsToken(parts[0]) || !IsToken(parts[1]))
        {
            return false;
        }

        var value = parts[2].Trim();

        if (value.Length == 0 || value.Any(char.IsControl))
        {
            return false;
        }

        condition = new SearchCondition(parts[0].Trim(), parts[1].Trim(), value);
        return true;
    }

    private static bool IsToken(string text)
    {
        var token = text.Trim();
        return token.Length is > 0 and <= 40 && token.All(char.IsAsciiLetterOrDigit);
    }
}
