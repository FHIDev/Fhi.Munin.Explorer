using System.Globalization;
using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>One field an avansert søk condition can test, as <c>GET api/explorer/variables/vilkar</c> lists it.</summary>
public sealed record ConditionField
{
    /// <summary>Free text: <c>inneholder</c>, <c>erLik</c>, <c>begynnerMed</c>, <c>inneholderIkke</c>.</summary>
    public const string TextType = "tekst";

    /// <summary>A day: <c>mellom</c> (a range), <c>for</c> and <c>etter</c>.</summary>
    public const string DateType = "dato";

    /// <summary>A code list where each row has one code.</summary>
    public const string SingleChoiceType = "enkeltvalg";

    /// <summary>A code list where each row can have several codes, so <c>harAlle</c> means something.</summary>
    public const string MultipleChoiceType = "flervalg";

    /// <summary>The operator whose value is a range of two days.</summary>
    public const string BetweenOperator = "mellom";

    // The date type's operators, for reading a condition whose field is not known.
    internal static readonly IReadOnlySet<string> DateOperators =
        new HashSet<string>([BetweenOperator, "for", "etter"], StringComparer.OrdinalIgnoreCase);

    /// <summary>The API's field token, for example <c>beskrivelse</c>.</summary>
    [JsonPropertyName("felt")] public string Field { get; init; } = "";

    /// <summary>One of <see cref="TextType"/>, <see cref="DateType"/>, <see cref="SingleChoiceType"/> or <see cref="MultipleChoiceType"/>.</summary>
    [JsonPropertyName("type")] public string Type { get; init; } = "";

    /// <summary>The operators this field takes, in the order the API lists them.</summary>
    [JsonPropertyName("operators")] public IReadOnlyList<string> Operators { get; init; } = [];

    /// <summary>The codes a code-list field takes, in display order; empty for the other types.</summary>
    [JsonPropertyName("options")] public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>Whether this field is a code list, whose value is codes rather than text or a day.</summary>
    [JsonIgnore] public bool IsCodeList => Type is SingleChoiceType or MultipleChoiceType;

    /// <summary>Whether the API will take <paramref name="condition"/> for this field: the same field, an operator it
    /// lists, and a value of the right shape. One the API refuses turns the whole search into a 400.</summary>
    public bool Accepts(SearchCondition condition)
    {
        if (!string.Equals(condition.Field, Field, StringComparison.OrdinalIgnoreCase)
            || !Operators.Contains(condition.Operator, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = condition.Value;

        return Type switch
        {
            TextType => value.Trim().Length is > 0 and <= SearchCondition.MaxTextLength,
            DateType => string.Equals(condition.Operator, BetweenOperator, StringComparison.OrdinalIgnoreCase)
                ? TryReadRange(value, out _, out _)
                : TryReadDay(value, out _),
            SingleChoiceType or MultipleChoiceType => Codes(value) is { Count: > 0 and <= SearchCondition.MaxCodes } codes
                && codes.All(code => Options.Contains(code, StringComparer.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    /// <summary>The codes in a code-list value, trimmed and blank ones left out.</summary>
    public static IReadOnlyList<string> Codes(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>A day in the API's form, <c>yyyy-MM-dd</c>.</summary>
    public static bool TryReadDay(string value, out DateOnly day) =>
        DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    /// <summary>A range in the API's form, <c>yyyy-MM-dd..yyyy-MM-dd</c>, the first day no later than the second.</summary>
    public static bool TryReadRange(string value, out DateOnly from, out DateOnly to)
    {
        to = default;
        var range = value.Split("..", 2);

        return TryReadDay(range[0], out from)
            && range.Length == 2
            && TryReadDay(range[1], out to)
            && from <= to;
    }
}
