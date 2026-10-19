using System.Globalization;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>A day as the dataperiode fields take and write it: dd.MM.yyyy, in either language.</summary>
/// <remarks>
/// Neither page language nor machine decides it (Fhi.Metadata-d2fbu). ISO is also taken, as a year
/// first reads one way only; 03/05/2024 is refused, being 5 March to an en-US reader.
/// </remarks>
internal static class DayFirstDate
{
    private const string Written = "dd.MM.yyyy";

    // Eight bare digits because inputmode="numeric" gives an iPhone no full stop to type.
    private static readonly string[] Accepted = [Written, "d.M.yyyy", "ddMMyyyy", "yyyy-MM-dd"];

    internal static bool TryParse(string? text, out DateOnly value) =>
        DateOnly.TryParseExact(text?.Trim(), Accepted, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    internal static string Format(DateOnly value) => value.ToString(Written, CultureInfo.InvariantCulture);
}
