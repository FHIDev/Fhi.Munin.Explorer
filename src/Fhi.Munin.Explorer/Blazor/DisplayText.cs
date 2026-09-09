namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Reading a catalogue string that may be blank rather than absent.</summary>
/// <remarks>
/// Shared by the search results, the saved-list view and that list's kilde tally, which all fall
/// back from one field to another and cannot spell it <c>??</c>: the Explorer API can send an
/// omitted string as <c>""</c>, which <c>??</c> keeps and the reader sees as nothing at all.
/// </remarks>
internal static class DisplayText
{
    /// <summary>Null for absent, empty or whitespace-only text, so <c>??</c> can fall back on it.</summary>
    internal static string? Trimmed(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
