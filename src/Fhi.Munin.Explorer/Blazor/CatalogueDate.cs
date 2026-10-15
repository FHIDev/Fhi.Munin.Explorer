namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A catalogue date as a reader reads it: one day, or a period with an open end shown as ongoing.
/// </summary>
/// <remarks>
/// Shared by the detail views, three of which had private copies that had already drifted apart.
/// One format, numeric, so no column needs a narrower one (Fhi.Metadata-n39ea, Fhi.Metadata-49ahq).
/// </remarks>
internal static class CatalogueDate
{
    /// <summary>A date as the day it fell on, written the way the date field takes one.</summary>
    /// <remarks>
    /// One rule for the page and the filter, <see cref="DateInput.Format(DateOnly, string?)"/>'s:
    /// <c>dd.MM.yyyy</c> for a Norwegian reader, as helsedata writes dates, ISO for an English one (Fhi.Metadata-49ahq).
    /// </remarks>
    internal static string Day(DateTimeOffset value, string? language) =>
        DateInput.Format(DateOnly.FromDateTime(value.DateTime), language);

    /// <summary>A day a reader chose rather than one the catalogue recorded.</summary>
    /// <remarks>
    /// The filter's chosen bound sits beside values that came through here, so it is written by the
    /// same rule (Fhi.Metadata-ufmop).
    /// </remarks>
    internal static string Day(DateOnly value, string? language) => DateInput.Format(value, language);

    /// <summary>A day the payload may not have carried at all, and null where it did not.</summary>
    /// <remarks>
    /// Null is what a fact list means by nothing to say, so the row reads "Ingen" as it does for a
    /// <see cref="Period"/> with no ends. Both absences reach here: Munin omitting the key, which the
    /// contracts read as null since Fhi.Metadata-se0by, and a <c>default</c> from any other source.
    /// </remarks>
    internal static string? DayOrNothing(DateTimeOffset? value, string? language) =>
        Written(value) is { } day ? Day(day, language) : null;

    /// <summary>The value, or null where it is one of the two ways of carrying no date.</summary>
    /// <remarks>
    /// Unwrapped before the comparison on purpose: <c>value == default</c> on a nullable compares
    /// against null rather than against MinValue, so it reads as a guard and is a no-op.
    /// </remarks>
    internal static DateTimeOffset? Written(DateTimeOffset? value) =>
        value is { } day && day != default ? day : null;

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
    internal static string? SourceSystemDay(string? value, string? language)
    {
        var raw = value?.Trim();

        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        return DateTime.TryParseExact(raw, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                                      System.Globalization.DateTimeStyles.None, out var day)
            ? Day(new DateTimeOffset(day, TimeSpan.Zero), language)
            : raw;
    }

    /// <summary>
    /// A period, with an unknown start shown as "?" and an open end as ongoing — neither as a
    /// blank nor as a guessed date.
    /// </summary>
    /// <remarks>
    /// An unknown start is written "?": an end standing alone reads as a start, and a bare en-dash
    /// as a value that failed to draw. A <c>default</c> at either end is no date there, as in
    /// <see cref="DayOrNothing"/> (Fhi.Metadata-msax9).
    /// </remarks>
    internal static string? Period(DateTimeOffset? from, DateTimeOffset? to, string? language,
                                   Texts texts)
    {
        from = Written(from);
        to = Written(to);

        if (from is null && to is null)
        {
            return null;
        }

        var start = from is { } f ? Day(f, language) : "?";
        var end = to is { } t ? Day(t, language) : texts.Ongoing;

        return $"{start} – {end}";
    }
}
