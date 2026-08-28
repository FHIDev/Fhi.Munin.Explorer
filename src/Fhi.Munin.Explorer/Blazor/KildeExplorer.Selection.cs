using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The row ticks, and the handover to the variable explorer they exist for.</summary>
/// <remarks>
/// The component has no NavigationManager and cannot reach the variable explorer, so the state
/// leaves through <see cref="ExploreVariablesRequested"/> and the host writes the URL. Kept out
/// of the URL itself, like the search text and the facets. (Fhi.Metadata-5ghur)
/// </remarks>
public sealed partial class KildeExplorer
{
    /// <summary>The kilder the reader has ticked, by id.</summary>
    /// <remarks>
    /// Not pruned when the search or the facets stop matching one: ticking three and then typing a
    /// word to find a fourth has unticked nothing. So the set can hold ticks the reader cannot see,
    /// which is why the bar counts them in words. (Fhi.Metadata-5ghur)
    /// </remarks>
    private readonly HashSet<Guid> _ticked = [];

    /// <summary>Whether the column and its bar are drawn, which is whether the host wired the
    /// handover.</summary>
    /// <remarks>
    /// A column over a button that leads nowhere costs the reader the work of choosing first. Reads
    /// false where the callback was created in a static parent and passed into an interactive
    /// island — it serialises as {"HasDelegate":true} and comes back empty. (Fhi.Metadata-5ghur)
    /// </remarks>
    private bool Selectable => ExploreVariablesRequested.HasDelegate;

    /// <summary>Whether this kilde is ticked.</summary>
    private bool IsTicked(Guid id) => _ticked.Contains(id);

    /// <summary>How many kilder are ticked, hidden ones included.</summary>
    private int TickedCount => _ticked.Count;

    /// <summary>Tick or untick one row.</summary>
    private void Tick(Guid id, bool ticked)
    {
        if (ticked)
        {
            _ticked.Add(id);
        }
        else
        {
            _ticked.Remove(id);
        }
    }

    /// <summary>Whether every row the reader can see is ticked — and there is at least one.</summary>
    /// <remarks>
    /// The emptiness matters: <c>All</c> over no rows is true, and a header checkbox that ticks
    /// itself when the search matched nothing tells the reader they have selected something.
    /// </remarks>
    private bool AllVisibleTicked(IReadOnlyList<KildeSummary> visible) =>
        visible.Count > 0 && visible.All(kilde => _ticked.Contains(kilde.Id));

    /// <summary>Tick every row the reader can see, or untick them when they already are.</summary>
    /// <remarks>
    /// Over the visible rows in both directions, so the control means the same thing whichever way
    /// it is pressed. No indeterminate state: it is a DOM property with no attribute behind it, and
    /// the count above the table says the same thing in words. (Fhi.Metadata-5ghur)
    /// </remarks>
    private void TickAllVisible(IReadOnlyList<KildeSummary> visible)
    {
        if (AllVisibleTicked(visible))
        {
            foreach (var kilde in visible)
            {
                _ticked.Remove(kilde.Id);
            }

            return;
        }

        foreach (var kilde in visible)
        {
            _ticked.Add(kilde.Id);
        }
    }

    /// <summary>Empty the selection, the ticks the current search has hidden included.</summary>
    private void ClearTicks() => _ticked.Clear();

    /// <summary>The ids the handover carries, which is not always the ids that are ticked.</summary>
    /// <remarks>
    /// Munin's three rules: ticks win outright, else the filtered rows travel, else nothing — which
    /// means the whole catalogue rather than a selection of none. Ordered by the list, not by when
    /// each was ticked, so two readers picking the same three produce the same link. (Fhi.Metadata-5ghur)
    /// </remarks>
    private IReadOnlyList<Guid> Handover(IReadOnlyList<KildeSummary> visible)
    {
        if (_ticked.Count > 0)
        {
            return [.. _kilder.Where(kilde => _ticked.Contains(kilde.Id)).Select(kilde => kilde.Id)];
        }

        return SearchText is null && ChosenCount == 0
            ? []
            : [.. visible.Select(kilde => kilde.Id)];
    }

    /// <summary>What the button says, which has to be what it is about to do.</summary>
    /// <remarks>
    /// The same three cases <see cref="Handover"/> answers, off the same two questions, so label and
    /// payload cannot disagree. Munin writes the first wording in all three; only the sentence
    /// differs here. (Fhi.Metadata-5ghur)
    /// </remarks>
    private string ExploreLabel =>
        _ticked.Count > 0 ? T.ExploreVariables
        : SearchText is null && ChosenCount == 0 ? T.ExploreAllVariables
        : T.ExploreFilteredVariables;

    /// <summary>Hand the selection to the host, which is what the ticks are for.</summary>
    /// <remarks>
    /// Through RaiseAsync, so a host handler that throws does not take the CMS page's circuit with
    /// it. The ticks are left alone: the host may not have navigated at all. (Fhi.Metadata-5ghur)
    /// </remarks>
    private Task ExploreVariablesAsync(IReadOnlyList<KildeSummary> visible) =>
        RaiseAsync(ExploreVariablesRequested, Handover(visible));
}
