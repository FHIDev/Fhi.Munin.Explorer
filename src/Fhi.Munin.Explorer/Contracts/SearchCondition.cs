using System.Diagnostics.CodeAnalysis;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>One avansert søk condition: a field, an operator and a value, all of which must hold.</summary>
/// <remarks>
/// <para>
/// On the wire it is the Explorer API's <c>vilkar=felt:operator:verdi</c>, repeated once per condition, and
/// <see cref="VariableFilter.Conditions"/> carries it to every call the filter goes to. <see cref="Field"/> and
/// <see cref="Operator"/> are the API's own tokens (<c>beskrivelse</c>, <c>inneholderIkke</c>), which
/// <see cref="IMuninExplorerClient.GetConditionFieldsAsync"/> lists with the operators each field takes.
/// </para>
/// <para>
/// The value is written as the API reads it: text as typed, a date as <c>yyyy-MM-dd</c>, a range as
/// <c>yyyy-MM-dd..yyyy-MM-dd</c>, and codes comma-separated. <see cref="ConditionField.Accepts"/> says whether a
/// condition is one the API will take.
/// </para>
/// </remarks>
/// <param name="Field">The API's field token, for example <c>navn</c> or <c>gyldigFra</c>.</param>
/// <param name="Operator">The API's operator token, for example <c>inneholder</c> or <c>erEnAv</c>.</param>
/// <param name="Value">The value in the API's form; never contains a line break.</param>
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

    /// <summary>
    /// Read the wire form back: everything after the second colon is the value. False for a field or operator that
    /// is blank or not a plain token, and for a blank value.
    /// </summary>
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
