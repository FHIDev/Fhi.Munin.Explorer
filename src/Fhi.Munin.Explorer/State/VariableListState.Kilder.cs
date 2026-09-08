using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.State;

/// <summary>One kilde the active list holds variables from, and how many of them it holds.</summary>
/// <param name="Id">The kilde, as the list's entries name it.</param>
/// <param name="Name">Its name, or its short name when the read model carries no long one.</param>
/// <param name="Count">
/// How many of the list's variables belong to it — over the whole list, never over a page.
/// </param>
public sealed record KildeInList(Guid Id, string Name, int Count);

/// <summary>
/// Which kilder the active list draws from, and which of them the reader has narrowed to.
/// </summary>
/// <remarks>
/// <para>
/// The tally is collected by the membership walk in <c>VariableListState.Membership.cs</c>, which
/// already reads every page of the active list — so it describes the whole list and cannot be
/// mistaken for the page on screen. A facet built from a page names kilder the reader cannot see
/// and hides ones they can, which for a 247-variable list looks entirely plausible.
/// </para>
/// <para>
/// The narrowing lives here rather than in either component because the two are in different
/// subtrees: the sidebar renders in the explorer's filter column and the rows in its tab panel, so
/// this service is the only channel between them. Narrowing is applied by the API, not here — see
/// <see cref="IMuninExplorerClient.GetMyListVariablesAsync"/>, whose paging stays the API's answer.
/// </para>
/// </remarks>
public sealed partial class VariableListState
{
    private readonly List<KildeInList> _kilder = [];

    /// <summary>
    /// Replaced on every change, never mutated in place.
    /// </summary>
    /// <remarks>
    /// A caller holds this while it awaits — the view carries it into a read that is in flight —
    /// and a set that changed underneath would make the request it describes disagree with the
    /// request it made. Rebuilding costs a handful of Guids; these are the kilder of one list.
    /// </remarks>
    private IReadOnlyCollection<Guid> _kildeFilter = [];

    /// <summary>
    /// The kilder in the active list, in no particular order. Empty until the walk finishes, which
    /// is why <see cref="KilderInListKnown"/> exists to tell that apart from a list with none.
    /// </summary>
    public IReadOnlyList<KildeInList> KilderInList => _kilder;

    /// <summary>
    /// Whether the walk that fills <see cref="KilderInList"/> has finished. False while it is
    /// running, and false when it was refused — a caller that read the empty tally as "no kilder"
    /// would say so about a list it never managed to read.
    /// </summary>
    public bool KilderInListKnown => _membershipLoaded;

    /// <summary>
    /// The kilder the reader has ticked, as a snapshot. Empty means every kilde, never none.
    /// </summary>
    public IReadOnlyCollection<Guid> KildeFilter => _kildeFilter;

    /// <summary>Whether the reader has ticked this kilde. A plain read, called on every render.</summary>
    public bool IsKildeChosen(Guid kildeId) => _kildeFilter.Contains(kildeId);

    /// <summary>
    /// Bumped by every change to <see cref="KildeFilter"/>, so a surface holding a page number can
    /// tell a narrowing from any of the other changes <c>Changed</c> reports and go back to page 1.
    /// </summary>
    public int KildeFilterVersion { get; private set; }

    /// <summary>Ticks the kilde, or unticks it when it is already ticked.</summary>
    public void ToggleKildeFilter(Guid kildeId)
    {
        var next = new HashSet<Guid>(_kildeFilter);

        if (!next.Remove(kildeId))
        {
            next.Add(kildeId);
        }

        _kildeFilter = next;
        KildeFilterVersion++;
        RaiseChanged(listId: null, affectsRows: true);
    }

    /// <summary>Unticks every kilde. Silent when none was ticked, so no surface re-reads for nothing.</summary>
    public void ClearKildeFilter()
    {
        if (_kildeFilter.Count == 0)
        {
            return;
        }

        _kildeFilter = [];
        KildeFilterVersion++;
        RaiseChanged(listId: null, affectsRows: true);
    }

    /// <summary>
    /// Drops the tally and the narrowing, for a switch of list or of reader.
    /// </summary>
    /// <remarks>
    /// The narrowing goes with the tally: a kilde ticked in one list is an id the next list may not
    /// hold at all, and left standing it would narrow that list to nothing with no ticked box on
    /// screen to explain it. The version moves too, so the view resets its page number.
    /// </remarks>
    private void ForgetKilder()
    {
        _kilder.Clear();

        if (_kildeFilter.Count > 0)
        {
            _kildeFilter = [];
            KildeFilterVersion++;
        }
    }
}
