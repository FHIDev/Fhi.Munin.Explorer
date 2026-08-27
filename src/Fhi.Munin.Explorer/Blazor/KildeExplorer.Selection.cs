using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The row ticks and what they are for: handing a set of kilder over to the variable explorer.
///
/// Separate from <c>KildeExplorer.Filters.cs</c> because the two narrow different things and must
/// not be read as one. The facets decide which rows the reader can see; the ticks are what the
/// reader picked out of what they saw, and neither is derived from the other.
/// </summary>
/// <remarks>
/// <para>
/// In Munin the handover is a link. Kelda builds
/// <c>https://runa.&lt;host&gt;/explorer?kildeIds=…&amp;kildeIds=…</c> — repeated query parameters,
/// with the explorer's own origin in front because it crosses a subdomain — and the reader clicks
/// it. None of that can be done from here. This component has no <c>NavigationManager</c>, no route
/// to the variable explorer and no idea what the host called the page it mounted one on; on
/// helsedata.no both explorers are components inside the host's own pages and the HOST owns the
/// URL. That is the question this work was blocked on, and the answer is the one the rest of this
/// component already gives: the state travels out through an
/// <see cref="Microsoft.AspNetCore.Components.EventCallback"/> and the host writes the URL.
/// </para>
/// <para>
/// The wire format is therefore not this component's business either. What
/// <see cref="ExploreVariablesRequested"/> carries is the ids themselves, and the host turns them
/// into whatever its own routing needs — <c>new VariableFilter { KildeIds = ids }.ToQueryString()</c>
/// is the pairing that lands them in <see cref="VariableExplorer.Filter"/>, and it is what makes
/// the two halves fit without either component knowing the other exists.
/// </para>
/// <para>
/// The ticks themselves are component state and are deliberately not two-way. That is the same
/// Kelda parity decision the search text and the facets follow — see the class remarks in
/// <c>KildeExplorer.razor.cs</c> — and Munin's own Kelda keeps its selection out of the URL too:
/// what is shareable there is the destination the selection produces, not the selection.
/// </para>
/// </remarks>
public sealed partial class KildeExplorer
{
    /// <summary>
    /// The kilder the reader has ticked, by id. A set rather than a list: ticking is idempotent,
    /// and the order a reader happened to tick rows in is not information the destination wants.
    /// </summary>
    /// <remarks>
    /// Not pruned when the search or the facets stop matching a ticked row. That is Munin's
    /// behaviour and the more defensible of the two: a reader who ticks three kilder and then types
    /// a word to go and find a fourth has not unticked anything. It does mean the set can hold
    /// ticks the reader cannot see, which is why the bar says how many are ticked rather than
    /// leaving them to be counted off the column.
    /// </remarks>
    private readonly HashSet<Guid> _ticked = [];

    /// <summary>
    /// Whether the selection column and its bar are drawn at all, which is whether the host wired
    /// <see cref="ExploreVariablesRequested"/>.
    /// </summary>
    /// <remarks>
    /// The ticks have one destination and this component cannot reach it alone. A host that has not
    /// wired the callback has no variable explorer for a selection to go to, and a checkbox column
    /// over a primary button that does nothing is worse than no column: the reader spends the work
    /// of choosing before finding out there was nowhere to take it.
    /// <para>
    /// One consequence is worth knowing, and it is in the host notes: this reads false wherever the
    /// callback was created in a statically-rendered parent and passed into an interactive island.
    /// An <see cref="Microsoft.AspNetCore.Components.EventCallback"/> is a struct rather than a
    /// delegate, so Blazor does not reject it — it serialises as <c>{"HasDelegate":true}</c> and
    /// comes back empty. Making this component's own mount point interactive is not the remedy;
    /// creating the callback inside an interactive component is. Until then the column is absent
    /// rather than dead, which is the better of the two failures and still a puzzle worth the
    /// paragraph — see the host notes and both samples' wrappers.
    /// </para>
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

    /// <summary>
    /// The header checkbox: tick every row the reader can see, or untick them when they already
    /// are.
    /// </summary>
    /// <remarks>
    /// Over the visible rows and no others, in both directions. Unticking takes the visible rows
    /// out of the set and leaves the rest of it standing, so a reader who ticked three kilder, then
    /// searched and cleared the one row that search left, still has the other two — which is the
    /// only reading under which the control means the same thing whichever way it is pressed.
    /// <para>
    /// There is no third, indeterminate state on the box, and that is a decision rather than an
    /// omission. <c>indeterminate</c> is a DOM property with no HTML attribute behind it, so the
    /// only way to set it from here is a JavaScript call on every render — a cost this component
    /// pays nowhere else, and one that reaches into the host's circuit to buy an appearance. What
    /// it would say is already said in words directly above the table, in a live region, by
    /// <see cref="Texts.SelectedKildeCount"/>: "3 kilder valgt". A sentence is what a screen reader
    /// announces reliably; a mixed checkbox is not.
    /// </para>
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

    /// <summary>
    /// The ids the handover carries, which is not always the ids that are ticked.
    /// </summary>
    /// <remarks>
    /// Munin's three cases, kept, because the reader's expectation is the same in both places and
    /// this is the half of the feature no reading of the markup can infer:
    /// <list type="number">
    /// <item>
    /// Ticked rows win outright. The reader marked those deliberately, so the facets do not get to
    /// narrow them further — a ticked kilde the current search has hidden still travels.
    /// </item>
    /// <item>
    /// With nothing ticked but a search or a facet in force, the rows the reader is looking at
    /// travel instead. Most of what Kelda filters on — kildetype, kategori, tilgangsnivå,
    /// databehandler — the variable explorer has no facet for, so carrying the ids the filter left
    /// is what reproduces the same scope on the other side whichever facet produced it.
    /// </item>
    /// <item>
    /// With neither, nothing travels. An empty list means the whole catalogue rather than a
    /// selection of none: a reader who has narrowed nothing is asking for the variable list as it
    /// comes.
    /// </item>
    /// </list>
    /// <para>
    /// Ordered by the list rather than by when each row was ticked. The set has no order to offer,
    /// and the API's own ordering is the one the reader saw — so two readers who tick the same
    /// three kilder in different orders produce the same link, which is what makes one shareable.
    /// </para>
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

    /// <summary>Hand the selection to the host, which is what the ticks are for.</summary>
    /// <remarks>
    /// Through <c>RaiseAsync</c> like every other callback here, so a host handler that throws does
    /// not take the CMS page's circuit down with it — and so a handler that navigates during static
    /// SSR still gets its <c>NavigationException</c> through.
    /// <para>
    /// The ticks are left alone afterwards. The host may not have navigated at all, and clearing
    /// them would mean coming back to a component that has forgotten what the reader chose.
    /// </para>
    /// </remarks>
    private Task ExploreVariablesAsync(IReadOnlyList<KildeSummary> visible) =>
        RaiseAsync(ExploreVariablesRequested, Handover(visible));
}
