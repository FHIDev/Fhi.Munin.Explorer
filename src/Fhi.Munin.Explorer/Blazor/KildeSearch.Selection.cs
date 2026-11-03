using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The row ticks, the datasamling marks in the drawers below them, and the handover to the variable
/// explorer the two exist for.
/// </summary>
/// <remarks>
/// <para>
/// The component has no NavigationManager and cannot reach the variable explorer, so the state
/// leaves through <see cref="ExploreVariablesRequested"/> and the host writes the URL. The ticks
/// themselves leave through <see cref="TickedKildeIdsChanged"/>. (Fhi.Metadata-5ghur, -nvf2w)
/// </para>
/// <para>
/// Ticks and marks are independent selections, and with any mark present they travel together
/// through <see cref="ExploreSelectionRequested"/> as one filter the API ORs (Fhi.Metadata-9huq2.13);
/// a host wiring only the obsolete <see cref="ExploreDatasamlingerRequested"/> still gets the union.
/// </para>
/// </remarks>
public sealed partial class KildeSearch
{
    /// <summary>The kilder the reader has ticked, by id.</summary>
    /// <remarks>
    /// Not pruned when the search or the facets stop matching one: ticking three and then typing a
    /// word to find a fourth has unticked nothing. So the set can hold ticks the reader cannot see,
    /// which is why the bar counts them in words. (Fhi.Metadata-5ghur)
    /// </remarks>
    private readonly HashSet<Guid> _ticked = [];

    /// <summary>
    /// The kilder ticked when the list opens. Set by the host, typically from its own URL; the
    /// component owns the selection afterwards.
    /// </summary>
    /// <remarks>
    /// Read once, on initialisation, as <see cref="Search"/> is, and only drawn where the handover
    /// is wired — see <see cref="ExploreVariablesRequested"/>. An id no kilde has is kept, as a tick
    /// hidden by the search is: the bar counts it and the handover leaves it out.
    /// </remarks>
    [Parameter] public IReadOnlyList<Guid>? TickedKildeIds { get; set; }

    /// <summary>
    /// Raised on every tick, untick and clear, with every id ticked. Gives a host
    /// <c>@bind-TickedKildeIds</c>.
    /// </summary>
    [Parameter] public EventCallback<IReadOnlyList<Guid>> TickedKildeIdsChanged { get; set; }

    /// <summary>
    /// The datasamlinger the reader has marked, as <c>&lt;kildeId&gt;:&lt;datasamlingId&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Read once, on initialisation, as <see cref="TickedKildeIds"/> is. The kilde is the top-level
    /// row that owns the panel the mark was made in — also for a datasamling under a delkilde — so
    /// the list can count and open the right row without fetching anything first. A value that is
    /// not two ids is dropped, and every row holding a mark opens itself on the first render.
    /// </remarks>
    [Parameter] public IReadOnlyList<string>? MarkedDatasamlinger { get; set; }

    /// <summary>
    /// Raised on every mark, unmark and clear, with every mark. Gives a host
    /// <c>@bind-MarkedDatasamlinger</c>.
    /// </summary>
    [Parameter] public EventCallback<IReadOnlyList<string>> MarkedDatasamlingerChanged { get; set; }

    /// <summary>
    /// Raised when the reader asks to explore variables for a selection holding marked
    /// datasamlinger, carrying the whole selection as one <see cref="VariableFilter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The filter holds the ticked kilder as <see cref="VariableFilter.KildeIds"/>, the marked
    /// datasamlinger alone as <see cref="VariableFilter.DatasamlingIds"/>, and
    /// <see cref="VariableFilter.OrTreeTicks"/>. With that set, Munin ORs the two lists, and a ticked
    /// kilde holding a mark narrows to its marked datasamlinger. <c>filter.ToQueryString()</c> is
    /// the pairing that lands it in <see cref="VariableSearch.Filter"/>.
    /// </para>
    /// <para>
    /// <b>Never raised together with <see cref="ExploreVariablesRequested"/>.</b> A selection with
    /// no marks is unchanged and still leaves through that one, with kilde ids only.
    /// </para>
    /// <para>
    /// A press is refused, with a sentence beside the button saying so, when either list holds more
    /// than 100 ids, which is as many as <see cref="VariableFilter.Parse"/> reads back, or the query
    /// would be longer than <see cref="MaxHandoverQueryLength"/> characters.
    /// </para>
    /// <para>
    /// Wire it, or no datasamling can be marked: the column, the per-row counts and the total are
    /// drawn only where this callback (or the obsolete <see cref="ExploreDatasamlingerRequested"/>)
    /// is wired as well, on <see cref="ExploreVariablesRequested"/>'s reasoning and with its trap: an
    /// <see cref="EventCallback"/> created in a statically rendered parent arrives empty.
    /// </para>
    /// </remarks>
    [Parameter] public EventCallback<VariableFilter> ExploreSelectionRequested { get; set; }

    /// <summary>
    /// Obsolete: wire <see cref="ExploreSelectionRequested"/> instead. Raised for a selection holding
    /// marked datasamlinger, carrying datasamling ids alone.
    /// </summary>
    /// <remarks>
    /// Used only where <see cref="ExploreSelectionRequested"/> is not wired, and then as before:
    /// each ticked kilde is fetched and expanded to every one of its datasamlinger. That can run to
    /// hundreds of ids, which is why it was replaced, and the press is refused past 100 ids here too.
    /// </remarks>
    [Obsolete("Use ExploreSelectionRequested, which carries the ticked kilder and only the marked datasamlinger. ExploreDatasamlingerRequested is removed in 3.0.0.")]
    [Parameter] public EventCallback<IReadOnlyList<Guid>> ExploreDatasamlingerRequested { get; set; }

    /// <summary>
    /// The longest query a selection may hand over, in characters, with or without marks.
    /// </summary>
    /// <remarks>
    /// helsedata's test site refuses an address over 8 KB with 414, and this leaves 1 KB of that for
    /// the host's own path. Two full lists of 100 ids would not fit, so the length is checked as well.
    /// </remarks>
    public const int MaxHandoverQueryLength = 7 * 1024;

    /// <summary>
    /// The marks, in the order they were made, each naming the row that owns the panel it sits in.
    /// </summary>
    /// <remarks>
    /// A list rather than a set, because the address is written from it and two readers marking the
    /// same two datasamlinger should produce the same link. Not pruned when the search hides the
    /// row, for <see cref="_ticked"/>'s reason. (Fhi.Metadata-75yov)
    /// </remarks>
    private readonly List<(Guid Kilde, Guid Datasamling)> _marked = [];

    /// <summary>Whether the column and its bar are drawn, which is whether the host wired the
    /// handover.</summary>
    /// <remarks>
    /// A column over a button that leads nowhere costs the reader the work of choosing first. Reads
    /// false where the callback was created in a static parent and passed into an interactive
    /// island — it serialises as {"HasDelegate":true} and comes back empty. (Fhi.Metadata-5ghur)
    /// </remarks>
    private bool Selectable => ExploreVariablesRequested.HasDelegate;

    /// <summary>
    /// Whether a datasamling can be marked, which is whether the host wired the second handover.
    /// </summary>
    /// <remarks>
    /// Both callbacks, not just the first: a mark has nowhere to go without a second one, and a
    /// checkbox over a button that would refuse it is worse than no checkbox.
    /// </remarks>
#pragma warning disable CS0618 // the obsolete handover still counts until 3.0.0 removes it
    private bool Markable => Selectable && (ExploreSelectionRequested.HasDelegate || ExploreDatasamlingerRequested.HasDelegate);
#pragma warning restore CS0618

    /// <summary>Whether marks travel as the old union of datasamlinger, which needs every ticked kilde fetched.</summary>
    private bool UsesUnion => !ExploreSelectionRequested.HasDelegate;

    /// <summary>Whether this kilde is ticked.</summary>
    private bool IsTicked(Guid id) => _ticked.Contains(id);

    /// <summary>How many kilder are ticked, hidden ones included.</summary>
    private int TickedCount => _ticked.Count;

    /// <summary>Whether this datasamling is marked, under the row that owns the panel it is in.</summary>
    private bool IsMarked(Guid kilde, Guid datasamling) => _marked.Contains((kilde, datasamling));

    /// <summary>How many of one row's datasamlinger are marked, drawn under its name.</summary>
    private int MarkCountFor(Guid kilde) => _marked.Count(mark => mark.Kilde == kilde);

    /// <summary>How many datasamlinger are marked anywhere, hidden rows included.</summary>
    private int TotalMarkCount => _marked.Count;

    /// <summary>Mark or unmark one datasamling.</summary>
    /// <remarks>
    /// The union the handover would send is topped up here rather than when the button is pressed,
    /// so a reader who marks and then presses is not made to wait on a fetch they could not see
    /// coming. (Fhi.Metadata-75yov)
    /// </remarks>
    private async Task MarkAsync(Guid kilde, Guid datasamling, bool marked)
    {
        if (marked)
        {
            if (!_marked.Contains((kilde, datasamling)))
            {
                _marked.Add((kilde, datasamling));
            }
        }
        else
        {
            _marked.Remove((kilde, datasamling));
        }

        await RaiseMarksAsync();
        await EnsureUnionAsync();
    }

    /// <summary>What <see cref="DatasamlingTable"/> needs to draw one row's marks, or null.</summary>
    private DatasamlingTable.Selection? MarksFor(Guid kilde) =>
        Markable
            ? new DatasamlingTable.Selection(
                datasamling => IsMarked(kilde, datasamling),
                (datasamling, marked) => MarkAsync(kilde, datasamling, marked),
                this)
            : null;

    /// <summary>Tick or untick one row.</summary>
    private async Task TickAsync(Guid id, bool ticked)
    {
        if (ticked)
        {
            _ticked.Add(id);
        }
        else
        {
            _ticked.Remove(id);
        }

        await RaiseTicksAsync();
        await EnsureUnionAsync();
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
    /// the count above the table says the same thing in words. (Fhi.Metadata-5ghur; exception: cjezd)
    /// </remarks>
    private async Task TickAllVisibleAsync(IReadOnlyList<KildeSummary> visible)
    {
        if (AllVisibleTicked(visible))
        {
            foreach (var kilde in visible)
            {
                _ticked.Remove(kilde.Id);
            }
        }
        else
        {
            foreach (var kilde in visible)
            {
                _ticked.Add(kilde.Id);
            }
        }

        await RaiseTicksAsync();
        await EnsureUnionAsync();
    }

    /// <summary>Empty the selection — both halves of it, and the ticks the search has hidden.</summary>
    /// <remarks>
    /// One control for both, because the reader made one selection: a reset that left the marks
    /// behind would leave a handover still promising them with the bar saying nothing is chosen.
    /// </remarks>
    private async Task ClearSelectionAsync()
    {
        _ticked.Clear();
        _marked.Clear();

        // The only place the offer may leave without stealing a focus: the press that empties the
        // selection is the reset's, and there is nothing left for a retry to fetch.
        _unionRetryShown = false;

        await RaiseTicksAsync();
        await RaiseMarksAsync();
    }

    private void SeedTicks() => _ticked.UnionWith(TickedKildeIds ?? []);

    /// <summary>The marks the host opened the list with, dropping what is not a pair of ids.</summary>
    /// <remarks>
    /// None at all where the second handover is unwired: a mark that cannot be made here has
    /// nowhere to go, and one seeded from the address would count in the bar, open rows and fetch
    /// for a button that would refuse the press. An id this catalogue has not is kept, not dropped.
    /// </remarks>
    private void SeedMarks()
    {
        if (!Markable)
        {
            return;
        }

        foreach (var value in MarkedDatasamlinger ?? [])
        {
            if (ParseMark(value) is { } mark && !_marked.Contains(mark))
            {
                _marked.Add(mark);
            }
        }
    }

    /// <summary>How many rows the address may open — and so fetch — by itself, marks and open rows together.</summary>
    /// <remarks>
    /// Each one is a request charged to the window helsedata's whole cluster shares, and the query
    /// is whatever a stranger typed — <see cref="UrlMirror.MaxValuesPerKey"/> of them at once. Past
    /// this the marks are still held, still counted and still travel. (Fhi.Metadata-75yov)
    /// </remarks>
    private const int MaxRowsOpenedByAddress = 20;

    /// <summary>The rows the address opens: those holding a mark, then those it names as open.</summary>
    /// <remarks>
    /// Filtered through <see cref="_kilder"/> for <see cref="TickedNeedingDatasamlinger"/>'s reason
    /// — an id this catalogue does not have, or one the row already says holds no datasamling,
    /// costs a round trip against the rate limit to learn what is known without asking. Marks
    /// come first: a mark the reader cannot see is a selection they cannot undo.
    /// </remarks>
    private IReadOnlyList<Guid> RowsToOpen()
    {
        HashSet<Guid> holding = [.. _kilder.Where(CanExpand).Select(kilde => kilde.Id)];

        return
        [
            .. _marked.Select(mark => mark.Kilde)
                      .Concat(_seededExpanded)
                      .Distinct()
                      .Where(holding.Contains)
                      .Take(MaxRowsOpenedByAddress)
        ];
    }

    /// <summary>One mark as the address spells it, or null when it is not a pair of ids.</summary>
    internal static (Guid Kilde, Guid Datasamling)? ParseMark(string value)
    {
        var separator = value.IndexOf(':', StringComparison.Ordinal);

        return separator > 0
               && Guid.TryParse(value[..separator], out var kilde)
               && Guid.TryParse(value[(separator + 1)..], out var datasamling)
            ? (kilde, datasamling)
            : null;
    }

    /// <summary>One mark as the address spells it — the pairing <see cref="ParseMark"/> reads.</summary>
    internal static string MarkValue(Guid kilde, Guid datasamling) => $"{kilde}:{datasamling}";

    private Task RaiseTicksAsync() => RaiseAsync(TickedKildeIdsChanged, (IReadOnlyList<Guid>)[.. _ticked], Log);

    private Task RaiseMarksAsync() =>
        RaiseAsync(MarkedDatasamlingerChanged,
                   (IReadOnlyList<string>)[.. _marked.Select(mark => MarkValue(mark.Kilde, mark.Datasamling))],
                   Log);

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
        _ticked.Count > 0 || _marked.Count > 0 ? T.ExploreVariables
        : SearchText is null && ChosenCount == 0 ? T.ExploreAllVariables
        : T.ExploreFilteredVariables;

    /// <summary>What the bar says the reader has chosen, in words, or nothing while they have not.</summary>
    /// <remarks>
    /// Two sentences and not one number: the ticks and the marks are independent, and a reader who
    /// has both has chosen two different kinds of thing. Either half alone reads as the one
    /// sentence it was before the other existed.
    /// </remarks>
    private string? SelectionLine(string? refusal)
    {
        var kilder = TickedCount > 0 ? T.SelectedKildeCount(TickedCount) : null;
        var marks = TotalMarkCount > 0 ? T.SelectedDatasamlingCount(TotalMarkCount) : null;

        var line = (kilder, marks) switch
        {
            (null, null) => null,
            (null, { } alone) => alone,
            ({ } alone, null) => alone,
            _ => kilder + " " + marks,
        };

        // In the live line rather than a note beside it, so the refusal is heard as it arrives.
        return refusal is null ? line : line is null ? refusal : line + " " + refusal;
    }

    /// <summary>The ticked kilder whose datasamlinger the union needs and has to fetch.</summary>
    /// <remarks>
    /// A kilde the list says holds none is left out rather than asked about: the answer is known
    /// from the row, and asking costs a round trip against the rate limit for an empty array.
    /// </remarks>
    private IEnumerable<KildeSummary> TickedNeedingDatasamlinger =>
        _kilder.Where(kilde => _ticked.Contains(kilde.Id) && kilde.DatasamlingCount > 0);

    /// <summary>The ticked kilder that hold no datasamling, which the note names.</summary>
    private IReadOnlyList<KildeSummary> TickedWithoutDatasamlinger =>
        [.. _kilder.Where(kilde => _ticked.Contains(kilde.Id) && kilde.DatasamlingCount == 0)];

    /// <summary>Whether the union is still waiting on a fetch, which is when the button is busy.</summary>
    private bool UnionPending =>
        _marked.Count > 0
        && UsesUnion
        && TickedNeedingDatasamlinger.Any(kilde => _datasamlingerLoading.Contains(kilde.Id));

    /// <summary>Whether a ticked kilde's datasamlinger could not be had, so the union is incomplete.</summary>
    private bool UnionFailed =>
        _marked.Count > 0
        && UsesUnion
        && TickedNeedingDatasamlinger.Any(kilde => _datasamlingerError.ContainsKey(kilde.Id));

    /// <summary>Whether the union's retry is on screen, which outlives the failure it answers.</summary>
    /// <remarks>
    /// A button leaving with the sentence it answers would drop the focus of the reader who just
    /// pressed it to &lt;body&gt; — the pager's rule, and the variable explorer's two retries'. It
    /// goes inert instead, and leaves only where the reset empties the selection under it.
    /// </remarks>
    private bool _unionRetryShown;

    /// <summary>Whether the press being answered is the union retry's own.</summary>
    /// <remarks>
    /// Nothing derived from the fetch can tell that press from any other: the retry drops the
    /// errors before it awaits, so <see cref="UnionFailed"/> is already false at the first yield
    /// and the sentence would leave mid-fetch saying the problem is solved. (Fhi.Metadata-75yov)
    /// </remarks>
    private bool _retryingUnion;

    /// <summary>Whether pressing the union's retry would do anything.</summary>
    private bool CanRetryUnion => UnionFailed && !UnionPending && !_retryingUnion;

    /// <summary>Whether the union's failure is still being reported, the retry's own run included.</summary>
    private bool UnionAlertShown => UnionFailed || _retryingUnion;

    /// <summary>What a mixed selection cannot reach, or nothing when the selection is not mixed.</summary>
    /// <remarks>
    /// Only with both halves present: with marks alone there is no kilde whose other variables the
    /// reader might have expected, and with ticks alone the handover is the one it always was.
    /// </remarks>
    private string? MixedSelectionNote
    {
        get
        {
            if (_marked.Count == 0 || _ticked.Count == 0 || !UsesUnion)
            {
                return null;
            }

            var empty = TickedWithoutDatasamlinger;

            return empty.Count == 0
                ? T.MixedSelectionNote
                : T.MixedSelectionNote + " "
                  + T.KilderWithoutDatasamlinger(string.Join(", ", empty.Select(kilde => RowName(kilde).Text)));
        }
    }

    /// <summary>
    /// Fetch what the union is missing, one ticked kilde at a time.
    /// </summary>
    /// <remarks>
    /// Nothing at all until something is marked, so a kilde-only selection costs the requests it
    /// always did. A kilde already in hand or already in flight is left alone, which is what keeps
    /// this to one call per ticked kilde however often the reader marks. (Fhi.Metadata-75yov)
    /// </remarks>
    private async Task EnsureUnionAsync()
    {
        if (_marked.Count == 0 || !UsesUnion)
        {
            return;
        }

        foreach (var kilde in TickedNeedingDatasamlinger.ToList())
        {
            if (!_datasamlinger.ContainsKey(kilde.Id) && !_datasamlingerLoading.Contains(kilde.Id))
            {
                await LoadDatasamlingerAsync(kilde.Id);
            }
        }

        _unionRetryShown |= UnionFailed;
    }

    /// <summary>Ask again for the ticked kilder whose datasamlinger did not arrive.</summary>
    /// <remarks>
    /// The cached detail goes with the error, because a kilde the catalogue does not publish is
    /// recorded as both a null detail and a failure: clearing the error alone would retire the
    /// warning having fetched nothing at all. (Fhi.Metadata-75yov)
    /// </remarks>
    private async Task RetryUnionAsync()
    {
        if (!CanRetryUnion)
        {
            return;
        }

        foreach (var kilde in TickedNeedingDatasamlinger.ToList())
        {
            if (_datasamlingerError.Remove(kilde.Id))
            {
                _datasamlinger.Remove(kilde.Id);
            }
        }

        _retryingUnion = true;

        try
        {
            await EnsureUnionAsync();
        }
        finally
        {
            _retryingUnion = false;
        }
    }

    /// <summary>Every datasamling of one kilde, direct and under a delkilde at any depth.</summary>
    /// <remarks>
    /// Off <see cref="DatasamlingGroups"/> rather than over the detail again, so a ticked kilde
    /// expands to exactly the rows its own drawer would have shown.
    /// </remarks>
    private IEnumerable<Guid> DatasamlingIdsOf(Guid kilde) =>
        DatasamlingGroups(kilde).SelectMany(group => group.Rows).Select(row => row.Id);

    /// <summary>
    /// The datasamlinger the handover carries: every one of a ticked kilde, plus every mark.
    /// </summary>
    /// <remarks>
    /// In the list's order and then the reader's, deduplicated, so two readers making the same
    /// selection produce the same link — <see cref="Handover"/>'s rule one level down.
    /// </remarks>
    private IReadOnlyList<Guid> DatasamlingHandover()
    {
        List<Guid> ids = [];
        HashSet<Guid> seen = [];

        foreach (var kilde in _kilder.Where(kilde => _ticked.Contains(kilde.Id)))
        {
            foreach (var datasamling in DatasamlingIdsOf(kilde.Id))
            {
                if (seen.Add(datasamling))
                {
                    ids.Add(datasamling);
                }
            }
        }

        foreach (var mark in _marked)
        {
            if (seen.Add(mark.Datasamling))
            {
                ids.Add(mark.Datasamling);
            }
        }

        return ids;
    }

    /// <summary>The ticked kilder and the marks, as the one filter the API ORs.</summary>
    /// <remarks>
    /// Ticks in the list's order and marks in the reader's, so two readers making the same
    /// selection produce the same link. A tick no kilde has is left out, as <see cref="Handover"/> does.
    /// </remarks>
    private VariableFilter SelectionFilter() =>
        new()
        {
            KildeIds = [.. _kilder.Where(kilde => _ticked.Contains(kilde.Id)).Select(kilde => kilde.Id)],
            DatasamlingIds = [.. _marked.Select(mark => mark.Datasamling).Distinct()],
            OrTreeTicks = true,
        };

    /// <summary>What a marked selection would hand over, as the query the host's address carries.</summary>
    private VariableFilter MarkedHandoverFilter() =>
        UsesUnion ? new VariableFilter { DatasamlingIds = DatasamlingHandover() } : SelectionFilter();

    /// <summary>The sentence a refused handover is said with, or null while the press would travel.</summary>
    private string? HandoverRefusal(IReadOnlyList<KildeSummary> visible)
    {
        if (_marked.Count > 0)
        {
            return TooLarge(MarkedHandoverFilter()) ? T.SelectionTooLarge : null;
        }

        if (!TooLarge(new VariableFilter { KildeIds = Handover(visible) }))
        {
            return null;
        }

        // With nothing ticked the rows on screen travel, so the way out is a narrower search, not fewer ticks.
        return _ticked.Count > 0 ? T.SelectionTooLarge : T.FilteredTooLarge;
    }

    // Past VariableFilter's own cap the variable explorer would open on fewer ids than were sent, and say nothing.
    private static bool TooLarge(VariableFilter filter) =>
        filter.KildeIds.Count > VariableFilter.MaxValuesPerFacet
        || filter.DatasamlingIds.Count > VariableFilter.MaxValuesPerFacet
        || filter.ToQueryString().Length > MaxHandoverQueryLength;

    /// <summary>Hand the selection to the host, which is what the ticks are for.</summary>
    /// <remarks>
    /// Through RaiseAsync, so a host handler that throws does not take the CMS page's circuit with
    /// it. The ticks are left alone: the host may not have navigated at all. (Fhi.Metadata-5ghur)
    /// </remarks>
    private Task ExploreVariablesAsync(IReadOnlyList<KildeSummary> visible)
    {
        if (HandoverRefusal(visible) is not null)
        {
            return Task.CompletedTask;
        }

        if (_marked.Count == 0)
        {
            return RaiseAsync(ExploreVariablesRequested, Handover(visible), Log);
        }

        if (!UsesUnion)
        {
            return RaiseAsync(ExploreSelectionRequested, SelectionFilter(), Log);
        }

        // A half-built union would hand over a narrower selection than the reader made, and say so
        // nowhere: the button reports both states beside itself instead of guessing.
#pragma warning disable CS0618 // the obsolete handover, for hosts that have not moved yet
        return UnionPending || UnionFailed
            ? Task.CompletedTask
            : RaiseAsync(ExploreDatasamlingerRequested, DatasamlingHandover(), Log);
#pragma warning restore CS0618
    }
}
