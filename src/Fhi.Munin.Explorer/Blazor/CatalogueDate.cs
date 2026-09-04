namespace Fhi.Munin.Explorer.Blazor;

/// <summary>How much room a date is allowed to take.</summary>
/// <remarks>
/// The caller's, because the room is the caller's: a spelled-out month wraps in a 320px sidebar and
/// does not in a main column. It never decides the ordinal dot, which follows the reader.
/// </remarks>
internal enum DateWidth
{
    /// <summary>The month spelled out: <c>1. januar 2026</c>, <c>1 January 2026</c>.</summary>
    Full,

    /// <summary>The month abbreviated, for a column too narrow for the spelled-out one.</summary>
    Narrow,
}

/// <summary>
/// A catalogue date as a reader reads it: one day, or a period with an open end shown as ongoing.
/// </summary>
/// <remarks>
/// Shared by the three detail views, whose private copies had already drifted apart. Two decisions
/// live here and only one is the caller's: the ordinal dot follows the reader, the month's width
/// follows the column (Fhi.Metadata-n39ea).
/// </remarks>
internal static class CatalogueDate
{
    /// <summary>A date as the day it fell on, in the reader's language.</summary>
    /// <remarks>
    /// The dot is what makes the number an ordinal in Norwegian; English writes the same date with
    /// none, so the pattern follows the reader rather than the culture merely supplying month names
    /// to a Norwegian skeleton. The culture's own long pattern leads with the weekday in English.
    /// </remarks>
    internal static string Day(DateTimeOffset value, string? language, DateWidth width = DateWidth.Full)
    {
        var month = width == DateWidth.Narrow ? "MMM" : "MMMM";
        var pattern = ReaderLanguage.IsEnglish(language) ? $"d {month} yyyy" : $"d. {month} yyyy";

        return value.ToString(pattern, CatalogueProperties.Culture(language));
    }

    /// <summary>A day the payload may not have carried at all, and null where it did not.</summary>
    /// <remarks>
    /// Null is what a fact list already means by nothing to say, so the row is dropped as it is for a
    /// <see cref="Period"/> with no ends. Both absences reach here: Munin omitting the key, which the
    /// contracts read as null since Fhi.Metadata-se0by, and a <c>default</c> from any other source.
    /// </remarks>
    internal static string? DayOrNothing(DateTimeOffset? value, string? language,
                                         DateWidth width = DateWidth.Full) =>
        value is { } day && day != default ? Day(day, language, width) : null;

    /// <summary>
    /// A source system's own date — <c>yyyyMMdd</c> — as a day, or verbatim when it is not one.
    /// </summary>
    /// <remarks>
    /// The catalogue writes these as text and writes junk among them, so anything that is not eight
    /// digits naming a real day is handed on unchanged rather than blanked: showing "20260231" as it
    /// stands gets it fixed at source, where an empty cell reads as a field nobody filled in. Kelda
    /// carries a round-trip check against calendar roll-over that this does not need — its parse is
    /// JavaScript's, which turns that same string into 2 March, and <c>TryParseExact</c> refuses it.
    /// </remarks>
    internal static string? SourceSystemDay(string? value, string? language,
                                            DateWidth width = DateWidth.Full)
    {
        var raw = value?.Trim();

        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        return DateTime.TryParseExact(raw, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                                      System.Globalization.DateTimeStyles.None, out var day)
            ? Day(new DateTimeOffset(day, TimeSpan.Zero), language, width)
            : raw;
    }

    /// <summary>
    /// A period, with an open end shown as ongoing rather than as a blank or a guessed date.
    /// </summary>
    /// <remarks>
    /// An end with no start stands alone: an en-dash with nothing before it reads as a value that
    /// failed to draw, and a start the catalogue never gave would be an invention.
    /// </remarks>
    internal static string? Period(DateTimeOffset? from, DateTimeOffset? to, string? language,
                                   Texts texts, DateWidth width = DateWidth.Full)
    {
        if (from is null && to is null)
        {
            return null;
        }

        var start = from is { } f ? Day(f, language, width) : "";
        var end = to is { } t ? Day(t, language, width) : texts.Ongoing;

        return string.IsNullOrEmpty(start) ? end : $"{start} – {end}";
    }
}
