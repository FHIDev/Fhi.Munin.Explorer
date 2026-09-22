namespace Fhi.Munin.Explorer.Blazor;

/// <summary>What one facet's cap leaves on screen, and how many values it is holding back.</summary>
/// <remarks>
/// The two together, from one pass, because the list a panel draws and the number on the control
/// beside it are two readings of one state: derived separately they can be taken at different
/// points of a render and offer to reveal a count that does not match what appears.
/// </remarks>
internal readonly record struct CappedValues<T>(IReadOnlyList<T> Visible, int Hidden);

/// <summary>What makes a facet long, for every filter panel this package draws.</summary>
/// <remarks>
/// One number and one predicate for three surfaces — Kelda's facets, the variabelutforsker's and
/// the saved list's — so a reader meets one facet behaviour rather than three, and so "long" cannot
/// come to mean one thing in one panel and another next door. It decides both halves at once: past
/// it a facet gets a search box of its own, and it is how many values are drawn before the rest go
/// behind "Vis N til".
/// </remarks>
internal static class FacetLimits
{
    /// <summary>How many values a facet has to have before a panel treats it as long.</summary>
    /// <remarks>
    /// Decided once and applied to every facet, never per facet: kildetype has five values and a
    /// box over five visible choices costs more attention than it saves, while databehandler runs
    /// to 39 and can be neither read nor scrolled past without one.
    /// </remarks>
    internal const int FacetSearchThreshold = 10;

    /// <summary>Whether a facet of <paramref name="count"/> values is long enough to be capped and searched.</summary>
    /// <remarks>
    /// The one predicate all three panels ask, rather than a copy of the comparison in each: two
    /// hand-kept readings of "long" are how a panel comes to cap a facet while withholding the
    /// control that lifts the cap.
    /// </remarks>
    internal static bool IsLong(int count) => count > FacetSearchThreshold;

    /// <summary>
    /// Whether a cap the reader lifted when a facet held <paramref name="asked"/> values still
    /// covers one that now holds <paramref name="count"/>.
    /// </summary>
    /// <remarks>
    /// A lifted cap outlives the values behind it: a facet is rebuilt whenever the query or the
    /// result set changes and keeps its key, so "show me all 39 databehandlere" would otherwise
    /// draw the next search's 80 uncapped — the tall panel this cap exists to prevent, back after
    /// any later search. A facet that has grown past what the reader agreed to see is capped again;
    /// one that has shrunk is not, since they already asked for more than it now holds.
    /// </remarks>
    internal static bool StillExpanded(int? asked, int count) => asked is { } many && count <= many;

    /// <summary>The values a panel draws — the first ten, and any past them the reader has ticked.</summary>
    /// <remarks>
    /// <paramref name="kept"/> says which values survive the cap wherever they sort: a panel that
    /// hid the reader's own choice would read as a filter dropped, while the results beside it
    /// stayed narrowed. What it lets through is discounted from <see cref="CappedValues{T}.Hidden"/>
    /// in the same pass, so the number on the control is what pressing it would actually add.
    /// </remarks>
    internal static CappedValues<T> Cap<T>(IReadOnlyList<T> values, bool expanded, Func<T, bool> kept)
    {
        if (expanded || !IsLong(values.Count))
        {
            return new CappedValues<T>(values, 0);
        }

        List<T> visible = [.. values.Take(FacetSearchThreshold)];
        var hidden = 0;

        foreach (var value in values.Skip(FacetSearchThreshold))
        {
            if (kept(value))
            {
                visible.Add(value);
            }
            else
            {
                hidden++;
            }
        }

        return new CappedValues<T>(visible, hidden);
    }

    /// <summary>A facet nothing is capping: everything on screen, nothing held back.</summary>
    internal static CappedValues<T> Uncapped<T>(IReadOnlyList<T> values) => new(values, 0);
}
