namespace Fhi.Munin.Explorer.Display;

/// <summary>Reading a catalogue string that may be blank rather than absent.</summary>
/// <remarks>
/// <para>
/// Shared by the search results, the saved-list view and that list's kilde tally, which all fall
/// back from one field to another and cannot spell it <c>??</c>: a string the Explorer API leaves
/// out arrives as <c>null</c> or as <c>""</c>, and <c>??</c> only catches the first. The one it
/// keeps reaches the reader as nothing at all.
/// </para>
/// <para>
/// In a folder and namespace of its own, not under <c>Blazor/</c>, for the reason
/// <see cref="Logging.ExplorerLog"/> is: <c>VariableListState</c> reads it too, and a
/// <c>State/</c> file reaching into the <c>Blazor</c> namespace is the inversion the folder split
/// exists to prevent.
/// </para>
/// </remarks>
internal static class DisplayText
{
    /// <summary>Null for absent, empty or whitespace-only text, so <c>??</c> can fall back on it.</summary>
    internal static string? Trimmed(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
