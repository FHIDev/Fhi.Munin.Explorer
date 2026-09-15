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

    /// <summary>The entries in the order they were added, which is the order the view draws them.</summary>
    internal IReadOnlyList<DetailTocEntry> Entries => _entries;

    /// <summary>Name the section with this id, under the same condition its block renders under.</summary>
    internal void Add(bool drawn, string id, string heading)
    {
        if (drawn)
        {
            _entries.Add(new DetailTocEntry(id, heading));
        }
    }

    /// <summary>Name a section that draws on every path the view draws anything at all.</summary>
    /// <remarks>
    /// Apart from <see cref="Add"/> so the call site says which of the predicates is not one,
    /// rather than passing a <c>true</c> a reader has to open the view to explain.
    /// </remarks>
    internal void Always(string id, string heading) => Add(true, id, heading);

    /// <summary>Name every section an explorer handed the view, at the place the view draws them.</summary>
    internal void AddNamed(IReadOnlyList<DetailNamedSection>? sections)
    {
        foreach (var section in sections ?? [])
        {
            _entries.Add(section.Entry);
        }
    }
}

/// <summary>Reading a built contents nav back, which is how a view asks whether to draw a block.</summary>
internal static class DetailTocEntries
{
    /// <summary>Whether the nav names this section, which is whether the view drew it.</summary>
    internal static bool Contains(this IReadOnlyList<DetailTocEntry> toc, string id) =>
        toc.Any(entry => entry.Id == id);
}
