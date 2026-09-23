using System.Globalization;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>A day as a reader types it into a date field, and as the field writes it back.</summary>
/// <remarks>
/// Invariant culture and explicit formats, so the server's culture never decides what a day is.
/// </remarks>
internal static class DateInput
{
    private const string NorwegianFormat = "dd.MM.yyyy";

    private const string IsoFormat = "yyyy-MM-dd";

    // Norwegian also takes ISO so a pasted date works; English also takes the dotted day first,
    // which no English reader mistakes for month first.
    private static readonly string[] NorwegianAccepted = [NorwegianFormat, "d.M.yyyy", IsoFormat];

    private static readonly string[] EnglishAccepted = [IsoFormat, NorwegianFormat, "d.M.yyyy"];

    internal static bool TryParse(string? text, string? language, out DateOnly value) =>
        DateOnly.TryParseExact(
            text?.Trim(),
            ReaderLanguage.IsEnglish(language) ? EnglishAccepted : NorwegianAccepted,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out value);

    internal static string Format(DateOnly value, string? language) =>
        value.ToString(ReaderLanguage.IsEnglish(language) ? IsoFormat : NorwegianFormat,
                       CultureInfo.InvariantCulture);

    /// <summary>An empty string for no date, so the field is drawn empty rather than left alone.</summary>
    internal static string Format(DateOnly? value, string? language) =>
        value is { } date ? Format(date, language) : "";
}
