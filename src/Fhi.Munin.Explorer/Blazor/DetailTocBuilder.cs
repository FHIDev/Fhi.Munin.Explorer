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

    /// <summary>
    /// The nav for a view whose sections are a resolved layout: one entry per section it drew, in
    /// that order, and the explorer's own sections after them.
    /// </summary>
    /// <remarks>
    /// Read off the layout rather than off the predicates that built it, so a link cannot point at
    /// a block left out. Shared because both detail views had a loop of this each
    /// (Fhi.Metadata-lr6yh).
    /// <para>
    /// Each heading's own <c>lang</c> goes with it: the words are the curator's, and a nav link
    /// repeating them unmarked is announced in the reader's phonetics (WCAG 3.1.2).
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<DetailTocEntry> For(
        IReadOnlyList<DetailLayoutSection> layout, IReadOnlyList<DetailNamedSection>? named)
    {
        DetailTocBuilder toc = new();

        foreach (var section in layout)
        {
            toc.Always(section.Id, section.Heading, section.HeadingLanguage);
        }

        toc.AddNamed(named);

        return toc.Entries;
    }
}
