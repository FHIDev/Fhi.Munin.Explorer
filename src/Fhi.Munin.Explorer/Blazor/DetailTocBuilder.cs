namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Collects a detail view's contents-nav entries, one call per section the view can draw.
/// </summary>
/// <remarks>
/// Here rather than as a local function in each of the three views, for the reason
/// <see cref="DetailSection"/> and <see cref="DetailBlocks"/> are: written three times, a change to
/// the shape reaches two of them and misses the third.
/// </remarks>
internal sealed class DetailTocBuilder
{
    private readonly List<DetailTocEntry> _entries = [];
    private readonly HashSet<string> _drawn = new(StringComparer.Ordinal);

    /// <summary>The entries in the order they were added, which is the order the view draws them.</summary>
    internal IReadOnlyList<DetailTocEntry> Entries => _entries;

    /// <summary>The view's own blocks that draw, kept apart so a named section's id cannot switch one on.</summary>
    internal IReadOnlySet<string> Drawn => _drawn;

    /// <summary>Name the section with this id, under the same condition its block renders under.</summary>
    /// <remarks>
    /// <paramref name="language"/> is the heading's own <c>lang</c>, which the entry carries for the
    /// reason <see cref="DetailTocEntry.Language"/> gives.
    /// </remarks>
    internal void Add(bool drawn, string id, string heading, string? language = null)
    {
        if (drawn)
        {
            _entries.Add(new DetailTocEntry(id, heading, language));
            _drawn.Add(id);
        }
    }

    /// <summary>Name a section that draws on every path the view draws anything at all.</summary>
    /// <remarks>
    /// Apart from <see cref="Add"/> so the call site says which of the predicates is not one,
    /// rather than passing a <c>true</c> a reader has to open the view to explain.
    /// </remarks>
    internal void Always(string id, string heading, string? language = null) =>
        Add(true, id, heading, language);

    /// <summary>Listed wherever the view draws them, which differs between the views.</summary>
    internal void AddNamed(IReadOnlyList<DetailNamedSection>? sections)
    {
        foreach (var section in sections ?? [])
        {
            _entries.Add(section.Entry);
        }
    }
}
