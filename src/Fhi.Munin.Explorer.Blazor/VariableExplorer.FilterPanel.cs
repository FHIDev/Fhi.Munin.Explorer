using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The facet sidebar: what can be narrowed, and what narrowing it costs.</summary>
public partial class VariableExplorer
{

    /// <summary>One facet, as the panel draws it: a disclosure holding a list of values.</summary>
    /// <remarks>
    /// <c>Key</c> is stable across renders, so the disclosure's open state stays with its own facet.
    /// <c>EmptyText</c> is what to say when the facet has no values; null means the facet is left out
    /// instead, which is the right answer for most of them, because a facet the API returned nothing
    /// for is one there is nothing to choose from. Variabelgruppe is the exception: its emptiness is
    /// a message.
    /// </remarks>
    private sealed record FacetGroup(
        string Key,
        string Label,
        bool OpenByDefault,
        IReadOnlyList<FacetValue> Values,
        string? EmptyText = null)
    {
        /// <summary>How many values in this facet are selected, counting nested ones.</summary>
        public int SelectedCount => Selected(Values);

        private static int Selected(IReadOnlyList<FacetValue> values) =>
            values.Sum(value => (value.Selected ? 1 : 0) + Selected(value.Children));
    }

    /// <summary>
    /// One value inside a facet, and the values nested under it.
    /// </summary>
    /// <remarks>
    /// <c>Count</c> is how many variables the value would leave, or null where there is no count to
    /// show. <c>Toggle</c> is what pressing it does, or null for a value that is not selectable —
    /// the kildetype headings the kilder are grouped under are labels rather than filters, because
    /// kildetype has a facet of its own.
    /// </remarks>
    private sealed record FacetValue(
        string Key,
        string Label,
        int? Count,
        bool Selected,
        Func<Task>? Toggle,
        IReadOnlyList<FacetValue> Children);

    /// <summary>A node on the way to becoming a <see cref="FacetValue"/> tree.</summary>
    /// <remarks>
    /// The delkilde, variabelgruppe and saved-filter facets all arrive as a flat list carrying a
    /// parent id, and all three become a tree the same way. This is the shape <see cref="Tree"/>
    /// works in so that rule lives in one place.
    /// </remarks>
    private sealed record TreeNode(Guid Id, Guid? ParentId, string Label, int Count);

    /// <summary>The facets on screen, in the order they are drawn.</summary>
    /// <remarks>
    /// Built from the last answer rather than cached, so a facet's selected state and its count can
    /// never describe two different moments. It is a few hundred records per render, which is the
    /// same order as the rows the component already renders.
    /// </remarks>
    private IReadOnlyList<FacetGroup> FacetGroups
    {
        get
        {
            if (_facets is not { } facets)
            {
                return [];
            }

            // Kildetype first and kilde second, which is the order helsedata's own variable page
            // puts them in; the rest follow Munin's explorer.
            List<FacetGroup> groups =
            [
                KildeTypeGroup(facets),
                KildeGroup(facets),
                VariabelgruppeGroup(facets),
                SavedFilterGroup(facets),
                DataTypeGroup(facets),
                HelsefagligKodeverkGroup(facets),
                AdministrativtKodeverkGroup(facets),
                InstrumentGroup(facets),
                OtherGroup(facets)
            ];

            // A facet the API returned nothing for is left out rather than drawn as an empty
            // disclosure — except where the emptiness is itself the message.
            return [.. groups.Where(group => group.Values.Count > 0 || group.EmptyText is not null)];
        }
    }

    /// <summary>The kildetype facet — one value each, and only one of them can be chosen.</summary>
    private FacetGroup KildeTypeGroup(FilterOptions facets) =>
        new("kildetype", T.FacetKildeType, OpenByDefault: true, [.. facets.KildeTyper.Select(KildeTypeValue)]);

    private FacetValue KildeTypeValue(KildetypeFacet type) =>
        new($"kildetype:{type.Value}",
            // The facet's own displayName is the raw enum name (SentraltHelseregister), so the
            // prose comes from the component's own translations and falls back to what the API said.
            T.KildeTypeLabel(type.Value, type.DisplayName),
            type.Count,
            string.Equals(_filter.KildeType, type.Value, StringComparison.OrdinalIgnoreCase),
            () => SetKildeTypeAsync(type.Value),
            []);

    /// <summary>
    /// The kilde facet: kilder grouped under their kildetype, each with its own delkilde tree.
    /// </summary>
    /// <remarks>
    /// The whole tree is built from the facet payload alone — <see cref="DelkildeFacet"/> carries
    /// both its parent delkilde and its kilde precisely so this needs no second request. The level
    /// below it, datasamling, is not in that payload at all and is therefore not drawn; reaching it
    /// would mean a hierarchy request per kilde whose counts are the kilde's own totals rather than
    /// counts cross-filtered against the current selection, which would put two kinds of number in
    /// one tree. <see cref="VariableFilter.DatasamlingIds"/> still filters when a host sets it.
    /// </remarks>
    private FacetGroup KildeGroup(FilterOptions facets)
    {
        var delkilderByKilde = facets.Delkilder.ToLookup(delkilde => delkilde.KildeId);

        // The order the kildetype facet is in, so the headings here and the facet above agree.
        var kildeTypeOrder = facets.KildeTyper
            .Select((type, index) => (type.Value, Index: index))
            .ToDictionary(entry => entry.Value, entry => entry.Index, StringComparer.OrdinalIgnoreCase);

        var grouped = facets.Kilder
            .GroupBy(KildeTypeKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => kildeTypeOrder.TryGetValue(group.Key, out var index) ? index : int.MaxValue)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => KildeTypeHeading(group, delkilderByKilde))
            .ToList();

        // With one kildetype in the list its heading says nothing the facet above does not — and it
        // is exactly one whenever a kildetype has been chosen, which is when the panel is most
        // crowded. So the kilder are lifted out of it.
        if (grouped.Count == 1)
        {
            return new FacetGroup("kilde", T.FieldSource, OpenByDefault: true, grouped[0].Children);
        }

        return new FacetGroup("kilde", T.FieldSource, OpenByDefault: true, grouped);
    }

    /// <summary>A kilde's kildetype, or the empty string when it has none — never null, so it can be a key.</summary>
    private static string KildeTypeKey(KildeFacet kilde) =>
        string.IsNullOrWhiteSpace(kilde.KildeType) ? "" : kilde.KildeType;

    /// <summary>A kildetype heading: a label rather than a filter, because kildetype has its own facet.</summary>
    private FacetValue KildeTypeHeading(
        IGrouping<string, KildeFacet> kilder,
        ILookup<Guid, DelkildeFacet> delkilderByKilde) =>
        new($"kildetype-group:{kilder.Key}",
            T.KildeTypeLabel(kilder.Key, kilder.Key),
            Count: null,
            Selected: false,
            Toggle: null,
            [.. kilder.Select(kilde => KildeValue(kilde, delkilderByKilde))]);

    private FacetValue KildeValue(KildeFacet kilde, ILookup<Guid, DelkildeFacet> delkilderByKilde) =>
        new($"kilde:{kilde.Id}",
            kilde.Name,
            kilde.Count,
            _filter.KildeIds.Contains(kilde.Id),
            () => ToggleAsync(_filter.KildeIds, kilde.Id, ids => _filter with { KildeIds = ids }),
            DelkildeChildren(kilde.Id, delkilderByKilde));

    private IReadOnlyList<FacetValue> DelkildeChildren(Guid kildeId, ILookup<Guid, DelkildeFacet> delkilderByKilde) =>
        Tree(delkilderByKilde[kildeId].Select(d => new TreeNode(d.Id, d.ParentDelkildeId, d.Name, d.Count)),
             "delkilde:",
             IsDelkildeChosen,
             ToggleDelkilde);

    private bool IsDelkildeChosen(Guid id) => _filter.DelkildeIds.Contains(id);

    private Func<Task> ToggleDelkilde(Guid id) =>
        () => ToggleAsync(_filter.DelkildeIds, id, ids => _filter with { DelkildeIds = ids });

    /// <summary>
    /// The variabelgruppe facet, as a tree.
    /// </summary>
    /// <remarks>
    /// Its empty state is a message rather than an omission. With nothing chosen in the source
    /// hierarchy the API answers this facet with a curated shortlist — the whole catalogue is 930
    /// per-kilde groups and useless as a starting point — and that shortlist is empty in every
    /// environment probed so far. Saying "pick a datakilde" is what stops an empty list from
    /// reading as a broken one.
    /// </remarks>
    private FacetGroup VariabelgruppeGroup(FilterOptions facets) =>
        new("variabelgruppe",
            T.FieldVariableGroup,
            OpenByDefault: false,
            Tree(facets.Variabelgrupper.Select(g => new TreeNode(g.Id, g.ParentId, g.Name, g.Count)),
                 "variabelgruppe:",
                 IsGruppeChosen,
                 ToggleGruppe),
            T.NoVariabelgrupper);

    private bool IsGruppeChosen(Guid id) => _filter.VariabelgruppeIds.Contains(id);

    private Func<Task> ToggleGruppe(Guid id) =>
        () => ToggleAsync(_filter.VariabelgruppeIds, id, ids => _filter with { VariabelgruppeIds = ids });

    /// <summary>The saved catalogue filters — see <see cref="FilterOptions.Filters"/> for why this is usually empty.</summary>
    private FacetGroup SavedFilterGroup(FilterOptions facets) =>
        new("filter",
            T.FacetFilter,
            OpenByDefault: false,
            Tree(facets.Filters.Select(f => new TreeNode(f.Id, f.ParentId, f.Name, f.Count)),
                 "filter:",
                 IsSavedFilterChosen,
                 ToggleSavedFilter));

    private bool IsSavedFilterChosen(Guid id) => _filter.FilterIds.Contains(id);

    private Func<Task> ToggleSavedFilter(Guid id) =>
        () => ToggleAsync(_filter.FilterIds, id, ids => _filter with { FilterIds = ids });

    private FacetGroup DataTypeGroup(FilterOptions facets) =>
        new("datatype", T.FacetDataType, OpenByDefault: false, [.. facets.DataTypes.Select(DataTypeValue)]);

    private FacetValue DataTypeValue(DataTypeFacet dataType) =>
        new($"datatype:{dataType.Value}",
            // The API returns the code with no label at all, so the prose is the component's own.
            T.DataTypeLabel(dataType.Value),
            dataType.Count,
            _filter.DataTypes.Contains(dataType.Value),
            () => ToggleAsync(_filter.DataTypes, dataType.Value, values => _filter with { DataTypes = values }),
            []);

    private FacetGroup HelsefagligKodeverkGroup(FilterOptions facets) =>
        new("helsefaglig-kodeverk",
            T.FacetHelsefagligKodeverk,
            OpenByDefault: false,
            [.. facets.HelsefagligKodeverk.Select(HelsefagligKodeverkValue)]);

    private FacetValue HelsefagligKodeverkValue(HelsefagligKodeverkFacet kodeverk) =>
        new($"hk:{kodeverk.ShortName}",
            kodeverk.ShortName,
            kodeverk.Count,
            _filter.HelsefagligKodeverk.Contains(kodeverk.ShortName),
            () => ToggleAsync(_filter.HelsefagligKodeverk, kodeverk.ShortName,
                              values => _filter with { HelsefagligKodeverk = values }),
            []);

    private FacetGroup AdministrativtKodeverkGroup(FilterOptions facets) =>
        new("administrativt-kodeverk",
            T.FacetAdministrativtKodeverk,
            OpenByDefault: false,
            [.. facets.AdministrativtKodeverk.Select(AdministrativtKodeverkValue)]);

    private FacetValue AdministrativtKodeverkValue(AdministrativtKodeverkFacet kodeverk) =>
        new($"ak:{kodeverk.Oid}",
            // The OID when fhi.kodeverk could not be reached, because a nameless button is worse
            // than one labelled with the number the filter actually sends.
            string.IsNullOrWhiteSpace(kodeverk.Name) ? kodeverk.Oid : kodeverk.Name,
            kodeverk.Count,
            _filter.AdministrativtKodeverk.Contains(kodeverk.Oid),
            () => ToggleAsync(_filter.AdministrativtKodeverk, kodeverk.Oid,
                              values => _filter with { AdministrativtKodeverk = values }),
            []);

    private FacetGroup InstrumentGroup(FilterOptions facets) =>
        new("instrument", T.FacetInstrument, OpenByDefault: false, [.. facets.Instruments.Select(InstrumentValue)]);

    private FacetValue InstrumentValue(InstrumentFacet instrument) =>
        new($"instrument:{instrument.Id}",
            string.IsNullOrWhiteSpace(instrument.Name) ? instrument.Code : instrument.Name,
            instrument.Count,
            _filter.InstrumentIds.Contains(instrument.Id),
            () => ToggleAsync(_filter.InstrumentIds, instrument.Id, ids => _filter with { InstrumentIds = ids }),
            []);

    /// <summary>The two filters that are a yes/no rather than a choice of values.</summary>
    private FacetGroup OtherGroup(FilterOptions facets) =>
        new("other",
            T.FacetOther,
            OpenByDefault: false,
            [
                new FacetValue("has-kildekodeverk", T.HasKildekodeverk, facets.KildeKodeverkCount,
                               _filter.HasKildekodeverk == true, ToggleKildekodeverkAsync, []),

                // No count of its own: the API reports no facet for it, and the number it would
                // change is the total, which the status line already states.
                new FacetValue("include-historical", T.IncludeHistorical, null,
                               _filter.IncludeHistorical, ToggleHistoricalAsync, [])
            ]);

    /// <summary>
    /// Turn a flat list of parented nodes into the tree the panel draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A node whose parent is not in the list is treated as a root rather than dropped. That is not
    /// a defensive flourish: the API cross-filters each facet, so a parent with no matching
    /// variables of its own is genuinely absent from a payload its children are in, and a child
    /// hung off a missing parent would be a filter the reader can neither see nor clear.
    /// </para>
    /// <para>
    /// A parent chain that loops back on itself — a self-parented node, or two nodes naming each
    /// other, neither of which the catalogue should ever produce — has no root to be reached from,
    /// so the walk seeds itself with whatever the first pass did not reach. Without that second
    /// pass a cycle and everything hanging off it vanishes from the panel silently, which is the
    /// same failure the orphan rule above exists to prevent, arriving by the other door. The walk
    /// remembers what it has already placed, so entering a cycle stops at the repeat rather than
    /// recursing until the stack runs out; that memory also keeps a duplicated id from being drawn
    /// twice.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<FacetValue> Tree(
        IEnumerable<TreeNode> nodes,
        string keyPrefix,
        Func<Guid, bool> selected,
        Func<Guid, Func<Task>> toggle)
    {
        var all = nodes.ToList();

        if (all.Count == 0)
        {
            return [];
        }

        var known = all.Select(node => node.Id).ToHashSet();
        var byParent = all.Where(node => node.ParentId is not null).ToLookup(node => node.ParentId!.Value);
        HashSet<Guid> placed = [];

        var rooted = all.Where(node => node.ParentId is not { } parent || !known.Contains(parent));

        List<FacetValue> roots = [.. rooted.Select(Build)];

        // Whatever the first pass could not reach: every member of a cycle has its parent present,
        // so none of them is a root, and dropping them would take a filter off the panel with no
        // error anywhere. Each one that is still unplaced becomes a root of its own, which places
        // the rest of its cycle underneath it.
        //
        // A foreach rather than AddRange over a query, because the test is against a set the body
        // mutates: for two nodes naming each other, building the first places the second, and the
        // second must not then be built as a root as well. Written as a query that would hold only
        // while nothing materialised it between the filter and the projection — and drawing one
        // node twice means two <li> siblings with the same key, which the renderer throws on.
        foreach (var node in all)
        {
            if (!placed.Contains(node.Id))
            {
                roots.Add(Build(node));
            }
        }

        return roots;

        FacetValue Build(TreeNode node)
        {
            placed.Add(node.Id);

            // Same shape as the second pass above, and for the same reason: each child is tested
            // against a set the recursion mutates, so building one sibling can place the next.
            List<FacetValue> children = [];

            foreach (var child in byParent[node.Id])
            {
                if (!placed.Contains(child.Id))
                {
                    children.Add(Build(child));
                }
            }

            return new FacetValue($"{keyPrefix}{node.Id}", node.Label, node.Count, selected(node.Id), toggle(node.Id), children);
        }
    }

    /// <summary>The legend over the whole panel, saying how many filters are in force.</summary>
    private string FiltersLegend => _filter.IsEmpty ? T.FiltersTitle : $"{T.FiltersTitle} ({_filter.ActiveCount})";

    /// <summary>A facet's own label, saying how many of its values are chosen.</summary>
    /// <remarks>
    /// On the summary line, so a collapsed facet still says that something inside it is narrowing
    /// the list. Without it the only sign of a filter chosen three disclosures down is the number of
    /// results changing.
    /// </remarks>
    private static string GroupLabel(FacetGroup group) =>
        group.SelectedCount == 0 ? group.Label : $"{group.Label} ({group.SelectedCount})";

    /// <summary>
    /// A facet's values as a nested list of toggle buttons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain <c>&lt;ul&gt;</c> with no class of its own, and buttons rather than checkboxes. Both
    /// follow the rule the rest of this component follows: no class name goes into the markup that
    /// cannot be read back off the host's stylesheet, and where there is nothing to read back the
    /// shape changes rather than a stylesheet appearing. Stiler has a square button and this
    /// component already renders one in two states, so a chosen value is a pressed button; a list
    /// is an element every base stylesheet styles, and its indentation is what draws the hierarchy
    /// without a class for a tree that nobody has verified.
    /// </para>
    /// <para>
    /// Every value is keyed. Counts move as the reader filters, so the values reorder between
    /// renders, and without keys the renderer would patch the button under the reader's finger into
    /// a different filter — leaving focus on a control that is no longer the one they pressed.
    /// </para>
    /// </remarks>
    private RenderFragment FacetList(IReadOnlyList<FacetValue> values) => builder =>
    {
        builder.OpenElement(0, "ul");

        foreach (var value in values)
        {
            builder.OpenElement(1, "li");
            builder.SetKey(value.Key);

            // Held in a local so the null check below is one the compiler can carry into the branch.
            var toggle = value.Toggle;

            if (toggle is null)
            {
                builder.AddContent(2, value.Label);
            }
            else
            {
                builder.OpenElement(3, "button");
                builder.AddAttribute(4, "class", FacetClass(value));
                builder.AddAttribute(5, "type", "button");

                // aria-pressed, and spelled out as "false" on the values that are not chosen —
                // unlike the sort buttons' aria-current, which is left off. The attribute is what
                // says these are toggles at all, so an unselected one carrying nothing would be
                // announced as an ordinary button that gives no sign of having two states.
                builder.AddAttribute(6, "aria-pressed", value.Selected ? "true" : "false");
                builder.AddAttribute(7, "onclick", EventCallback.Factory.Create(this, toggle));
                builder.AddContent(8, FacetText(value));
                builder.CloseElement();
            }

            if (value.Children.Count > 0)
            {
                builder.AddContent(9, FacetList(value.Children));
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    };

    /// <summary>A value's visible text — its label, and the count of what it would leave.</summary>
    /// <remarks>
    /// The count is in the button's own text rather than in a badge beside it, so it is part of the
    /// accessible name: "Dødsårsaksregisteret (1 234)" is announced whole, where a separate element
    /// would be read as a stray number or skipped.
    /// </remarks>
    private static string FacetText(FacetValue value) =>
        value.Count is { } count ? $"{value.Label} ({count})" : value.Label;

    /// <summary>A value's classes — filled when chosen, a ghost when not, the same pair the sort buttons use.</summary>
    private static string FacetClass(FacetValue value)
    {
        var style = value.Selected ? "button-square--secondary" : "button-square--ghost";

        return $"hd-button-square {style} margin-right margin-bottom";
    }

    /// <summary>Add or remove one value from a facet, and fetch what that leaves.</summary>
    /// <remarks>
    /// The type parameter is <c>TItem</c> and not <c>T</c>, which is the component's own
    /// translations accessor: a <c>T</c> here would shadow it, and the first string this body ever
    /// needs would fail to compile with an error pointing at the type parameter instead.
    /// </remarks>
    private Task ToggleAsync<TItem>(
        IReadOnlyList<TItem> selected, TItem value, Func<IReadOnlyList<TItem>, VariableFilter> apply)
    {
        if (selected.Contains(value))
        {
            return ApplyFilterAsync(
                apply([.. selected.Where(chosen => !EqualityComparer<TItem>.Default.Equals(chosen, value))]));
        }

        return ApplyFilterAsync(apply([.. selected, value]));
    }

    /// <summary>
    /// Choose a kildetype, or clear it by choosing the one already chosen.
    /// </summary>
    /// <remarks>
    /// One at a time, because the API takes one. Pressing the chosen one again clears it, which is
    /// what the button's own aria-pressed promises — a radio group would say the choice cannot be
    /// undone, and there is no "any kildetype" value to go back to.
    /// </remarks>
    private Task SetKildeTypeAsync(string value)
    {
        var chosen = string.Equals(_filter.KildeType, value, StringComparison.OrdinalIgnoreCase);

        return ApplyFilterAsync(_filter with { KildeType = chosen ? null : value });
    }

    /// <summary>
    /// Keep only variables that have a kildekodeverk link, or stop filtering on it.
    /// </summary>
    /// <remarks>
    /// Two states, not three. The API's <c>false</c> — only variables *without* one — is a question
    /// nobody asked of a catalogue browser, and offering it from one button would make a single
    /// press mean "yes", "no" or "either depending on where you are in the cycle".
    /// </remarks>
    private Task ToggleKildekodeverkAsync() =>
        ApplyFilterAsync(_filter with { HasKildekodeverk = _filter.HasKildekodeverk == true ? null : true });

    private Task ToggleHistoricalAsync() =>
        ApplyFilterAsync(_filter with { IncludeHistorical = !_filter.IncludeHistorical });

    /// <summary>Drop every filter and fetch the whole search again.</summary>
    /// <remarks>
    /// Always on screen, and inert rather than absent when there is nothing to clear — the same
    /// treatment the pager's buttons get, and for the same reason: taking the control the reader
    /// just pressed out of the document drops focus to <c>&lt;body&gt;</c>. Pressing it with no
    /// filters set asks for the filter already in force, which <see cref="ApplyFilterAsync"/>
    /// returns from without a request.
    /// </remarks>
    private Task ClearFiltersAsync() => ApplyFilterAsync(VariableFilter.None);

    /// <summary>
    /// Apply <paramref name="next"/>: fetch what it leaves, and refresh the counts beside it.
    /// </summary>
    /// <remarks>
    /// The one way the filter ever changes, so the rules that go with changing it — back to page
    /// one, roll back a fetch that failed, tell the host what is actually in force — are written
    /// once rather than once per facet.
    /// </remarks>
    private async Task ApplyFilterAsync(VariableFilter next)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit, a
        // sort click and a page turn.
        if (_loading)
        {
            return;
        }

        // Also what makes the clear button inert when there is nothing to clear. VariableFilter
        // compares by what it narrows, not by the identity of its lists — see the note on it.
        if (next == _filter)
        {
            return;
        }

        var previous = _filter;

        _filter = next;

        // Narrowing renumbers every page, so the page the reader is on is no longer the same rows.
        _page = 1;
        _keepPager = false;

        // _executedSearch, not _search: a click blurs the search field first, so the box's contents
        // have already been written to _search — text the reader may never have submitted. Same
        // reason the sort buttons fetch with it.
        if (await FetchAsync(_executedSearch))
        {
            // Only on success. The counts describe a selection, and after a rollback the selection
            // they already describe is the one back in force.
            await FetchFacetsAsync();
        }
        else
        {
            // The rows on screen are still the old ones, so the buttons have to say so — the same
            // invariant the sort rollback protects.
            _filter = previous;
        }

        // _filter and not next: what the host is told is what is in force, rolled back or not.
        await RaiseAsync(FilterChanged, _filter);
    }

    /// <summary>
    /// Refresh the facets and their counts for the current search and filter.
    /// </summary>
    /// <remarks>
    /// Its own request, and its own failure. The counts are cross-filtered against the whole
    /// selection, so they move whenever the search or the filter does — but not when the page or
    /// the ordering does, which is why turning a page does not re-ask for them.
    /// <para>
    /// A failure keeps the facets already on screen rather than clearing them. They are the controls
    /// the reader is using, and the numbers being briefly stale is a far smaller problem than the
    /// panel emptying under a press.
    /// </para>
    /// </remarks>
    private async Task FetchFacetsAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            // The component's own language, so the datatype names come back in the language the
            // rest of the component is rendering in.
            _facets = await Client.GetFiltersAsync(_executedSearch, _filter, Language);
            _facetError = null;
        }
        catch (Exception)
        {
            _facetError = T.FilterError;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Open this row's detail panel, or close it when it is the one already open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One panel at a time. Opening a second row closes the first, which is what keeps the
    /// component to one fetched detail and one selection to report to the host — and what stops a
    /// long list from turning into a page of expanded cards nobody can find their way back through.
    /// </para>
    /// <para>
    /// Not dropped while a list fetch is in flight, unlike a sort or a page turn. Those all ask the
    /// same question of the same endpoint and would race each other; this one asks a different
    /// endpoint about a row that is already on screen, and making the reader wait for a slow search
    /// before a card will open would be a delay with nothing behind it. If the search does replace
    /// the rows underneath, the selection goes with them — see
    /// <see cref="DropSelectionIfGoneAsync"/>.
    /// </para>
    /// </remarks>
    private async Task ToggleDetailAsync(VariableSummary v)
    {
        if (IsSelected(v))
        {
            ClearSelection();
            await RaiseAsync<Guid?>(SelectedVariableIdChanged, null);

            return;
        }

        _selectedId = v.Id;

        // Back to the first tab for the newly opened row. A reader who was on Data for one variable
        // has not asked to be on Data for the next, and arriving on a tab you did not choose — with
        // different content under it — reads as the panel having lost your place.
        _tab = PanelTab.Details;

        await LoadDetailAsync(v.Id);

        // _selectedId rather than v.Id: the fetch above yields, so another row may have been opened
        // while it ran, and what the host is told has to be what is open — the same rule
        // FilterChanged follows after a rollback.
        await RaiseAsync(SelectedVariableIdChanged, _selectedId);
    }

    /// <summary>
    /// Fetch the detail for <paramref name="id"/> into the open panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every write back into the component is guarded by the generation this call claimed still
    /// being the current one — not by the id, which names the variable and not the call, so it
    /// cannot tell two fetches for the same row apart. Two rows opened in quick succession are two
    /// requests in flight, and
    /// nothing says the first one answers first — without the guard the slower answer would paint
    /// itself under the other row's heading, which is a panel describing a variable the reader is
    /// not looking at rather than a visibly broken one.
    /// </para>
    /// <para>
    /// The historical variables the filter is showing are asked for here too. The endpoint hides
    /// them by default, so a reader who turned "Vis historiske" on would otherwise be told that a
    /// row they can see does not exist.
    /// </para>
    /// <para>
    /// Null is not a failure — <see cref="IMuninExplorerClient"/> answers it for something that is
    /// not published — so it is reported as "not found" rather than as "try again in a moment",
    /// which is advice that would never come good.
    /// </para>
    /// </remarks>
    private async Task LoadDetailAsync(Guid id)
    {
        // Claimed before anything is written, and never reused: ownership of the panel is per call,
        // which is what the guards below compare against.
        var generation = ++_detailGeneration;

        _detail = null;
        _detailError = null;
        _detailLoading = true;

        // The owner panel is drawn from the detail being replaced, so it cannot survive the
        // replacement — opening a second row with a kilde disclosed would otherwise show that
        // kilde under the new variable's name until its own fetch landed.
        ClearSource();

        // Neither can the code lists, and the reason is sharper: the codes are fetched per variable
        // as well as per reference, so a cache kept across the replacement would answer the new
        // variable's kodeverk with the old one's codes rather than merely looking out of place.
        ClearCodes();

        StateHasChanged();

        try
        {
            var detail = await Client.GetVariableAsync(id, includeHistorical: _filter.IncludeHistorical);

            if (_detailGeneration != generation)
            {
                return;
            }

            _detail = detail;
            _detailError = detail is null ? T.DetailMissing : null;
        }
        catch (Exception)
        {
            if (_detailGeneration == generation)
            {
                // Said in the panel, not in the component's alert region: the rows are unaffected.
                _detailError = T.DetailError;
            }
        }
        finally
        {
            // Only when this call still owns the panel. A later selection has already set the flag
            // for its own fetch, and clearing it here would report that one as finished.
            if (_detailGeneration == generation)
            {
                _detailLoading = false;
            }
        }
    }

    /// <summary>Close the panel and forget what was fetched for it.</summary>
    private void ClearSelection()
    {
        _tab = PanelTab.Details;

        _selectedId = null;
        _detail = null;
        _detailError = null;

        // Closing is what disowns a fetch still in flight for the row that was open — the id it was
        // made for can come back, but the generation it claimed cannot.
        _detailGeneration++;

        // Cleared as well, because that abandoned fetch will not clear it: its own guard keeps it
        // from writing anything back at all.
        _detailLoading = false;

        // The owner panel hangs inside the panel being closed, so it goes with it. Left behind it
        // would be a kilde nothing draws, and the next variable opened would inherit it.
        ClearSource();

        // The code lists hang in it too, and for them "inherited by the next variable" is worse
        // than a stray panel: two variables can share a reference, so a cache left behind would
        // look right and be another variable's answer.
        ClearCodes();
    }

    /// <summary>Close the kilde or datasamling panel and forget what was fetched for it.</summary>
    private void ClearSource()
    {
        _sourceKind = null;
        _kilde = null;
        _datasamling = null;
        _sourceError = null;

        // Same reason ClearSelection bumps the detail's: closing disowns a fetch still in flight,
        // and the generation it claimed cannot come back even though the id can.
        _sourceGeneration++;
        _sourceLoading = false;
    }

    /// <summary>
    /// Open the kilde or the datasamling the variable belongs to, or close the one already open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One owner at a time, the same rule the variable panels follow: pressing Datasamling with
    /// Datakilde open swaps the panel rather than stacking a second one inside a result card.
    /// </para>
    /// <para>
    /// The id comes from the variable's own detail, which is the payload the buttons are rendered
    /// from — so a press can only ever ask for an owner the open panel names. It is re-read here
    /// rather than captured in the callback because <see cref="_detail"/> is what the panel is
    /// drawn from: an owner fetched for a variable that is no longer the open one would paint
    /// itself under the wrong heading.
    /// </para>
    /// </remarks>
    private async Task ToggleSourceAsync(SourceKind kind)
    {
        if (SourceOpen(kind))
        {
            ClearSource();

            return;
        }

        if (_detail is not { } detail || SourceIdOf(detail, kind) is not { } id)
        {
            return;
        }

        _sourceKind = kind;
        await LoadSourceAsync(kind, id);
    }

    /// <summary>
    /// Fetch one owner into the open panel.
    /// </summary>
    /// <remarks>
    /// Guarded per call rather than per id, for the reason <see cref="LoadDetailAsync"/> is: the
    /// two endpoints are different but the hazard is the same, and swapping between Datakilde and
    /// Datasamling twice quickly is two calls whose answers can arrive in either order. Null is
    /// "the catalogue does not publish this", not a failure, so it is reported as "not found"
    /// rather than as advice to try again.
    /// </remarks>
    private async Task LoadSourceAsync(SourceKind kind, Guid id)
    {
        var generation = ++_sourceGeneration;

        _kilde = null;
        _datasamling = null;
        _sourceError = null;
        _sourceLoading = true;
        StateHasChanged();

        try
        {
            if (kind == SourceKind.Kilde)
            {
                var kilde = await Client.GetKildeAsync(id);

                if (_sourceGeneration != generation)
                {
                    return;
                }

                _kilde = kilde;
                _sourceError = kilde is null ? T.KildeMissing : null;
            }
            else
            {
                var datasamling = await Client.GetDatasamlingAsync(id);

                if (_sourceGeneration != generation)
                {
                    return;
                }

                _datasamling = datasamling;
                _sourceError = datasamling is null ? T.DatasamlingMissing : null;
            }
        }
        catch (Exception)
        {
            if (_sourceGeneration == generation)
            {
                // Said in the owner panel, not in the variable's above it and not in the
                // component's alert region: neither the rows nor the variable is stale because the
                // kilde endpoint was unreachable.
                _sourceError = kind == SourceKind.Kilde ? T.KildeError : T.DatasamlingError;
            }
        }
        finally
        {
            if (_sourceGeneration == generation)
            {
                _sourceLoading = false;
            }
        }
    }

    /// <summary>
    /// Close the panel when the variable it belongs to is no longer among the rows on screen.
    /// </summary>
    /// <remarks>
    /// The panel is drawn inside its own row, so a selection the current result does not contain is
    /// one nothing renders — state the reader cannot see and cannot get rid of, which would come
    /// back the moment they paged past that row again. Run after every result that arrives, so a
    /// new search, a filter, a reordering and a page turn are all covered by one rule rather than
    /// four. The host is told, because a URL naming a variable the page is not showing hands out a
    /// link that opens something else.
    /// </remarks>
    private async Task DropSelectionIfGoneAsync()
    {
        if (_selectedId is not { } id || IsOnScreen(id))
        {
            return;
        }

        ClearSelection();
        await RaiseAsync<Guid?>(SelectedVariableIdChanged, null);
    }

    private bool IsOnScreen(Guid id) => _result?.Items.Any(v => v.Id == id) is true;

    /// <summary>
    /// Open the panel the host asked for, once the first result is known.
    /// </summary>
    /// <remarks>
    /// After the search rather than before it, because whether the id is worth fetching depends on
    /// whether the row is there to draw it in. Whether it is there is not asked here, though:
    /// <see cref="FetchAsync"/> runs <see cref="DropSelectionIfGoneAsync"/> after every fetch,
    /// failed or answered, so a selection the first result does not hold has already been closed
    /// and reported as null by the time this runs. A selection still set is a row on screen, and
    /// the only thing left to do with it is fetch it.
    /// </remarks>
    private async Task OpenInitialSelectionAsync()
    {
        if (_selectedId is not { } id)
        {
            return;
        }

        await LoadDetailAsync(id);
    }

    protected override async Task OnInitializedAsync()
    {
        _search = Search;
        _filter = Filter ?? VariableFilter.None;
        _selectedId = SelectedVariableId;
        await SearchAsync();
        await OpenInitialSelectionAsync();
    }

    private async Task SearchAsync()
    {
        // Nothing disables the submit button while a search runs — see the comment on it in
        // the markup — so a second submit is dropped here instead.
        if (_loading)
        {
            return;
        }

        // A different search is a different result set; page 7 of the old one means nothing in it.
        _page = 1;
        _keepPager = false;

        // The live contents of the box, which is what submitting means.
        if (await FetchAsync(_search))
        {
            // The counts are cross-filtered against the search as well as the filter, so a new
            // search moves them; only on success, so a failed search leaves the numbers describing
            // the rows that are still on screen.
            await FetchFacetsAsync();
        }

        await NotifySearchChangedAsync();
    }

    /// <summary>
    /// Sort by <paramref name="sort"/>: the active field again reverses the direction, another
    /// field starts ascending. Runa's rule, moved off the column header it used to live on.
    /// </summary>
    private async Task SortAsync(SortField sort)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit. The
        // guard comes first on purpose: changing the state and then not fetching would leave a
        // button saying the list is ordered one way while it is still ordered the other.
        if (_loading)
        {
            return;
        }

        // Kept so a failed fetch can put them back — see below.
        var previousSort = _sort;
        var previousDirection = _direction;

        if (sort == _sort)
        {
            _direction = _direction == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            _sort = sort;
            _direction = SortDirection.Ascending;
        }

        // Reordering renumbers every page, so the page the user is on is no longer the same rows.
        _page = 1;
        _keepPager = false;

        // _executedSearch, not _search. Sorting is not searching: a click blurs the field first, so
        // by the time this runs the box's contents have already been written to _search — text the
        // user may never have submitted. Fetching with it would run a search nobody asked for,
        // quietly, under a status line that then described the accidental search instead of saying
        // anything moved. It would also desynchronise the host, whose URL only follows SearchChanged.
        if (!await FetchAsync(_executedSearch))
        {
            // The same invariant the _loading guard above protects, on the path that guard cannot
            // see: the list is still in the old order, so the buttons have to say so. Left moved,
            // they would claim an order the API never delivered — and pressing the same button
            // again would take the reversal branch and ask for descending, with no way back to the
            // ascending fetch that just failed short of cycling twice.
            _sort = previousSort;
            _direction = previousDirection;
        }
    }

    /// <summary>
    /// Show page <paramref name="page"/> of the current result, keeping the search and the order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one way the page number ever changes, which is what the pager's two buttons, the clamp
    /// and a future URL-backed page all go through. Both buttons hand it an out-of-range number at
    /// the ends of the list rather than being guarded at the call site, so the boundary is enforced
    /// once, here, instead of once per caller.
    /// </para>
    /// <para>
    /// Not a search, so <see cref="SearchChanged"/> is not raised: the host's URL follows what was
    /// searched for, and turning a page did not change that.
    /// </para>
    /// </remarks>
    private async Task GoToPageAsync(int page)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit and a
        // sort click — and for the same reason the buttons carry aria-disabled instead of disabled:
        // neither is taken out of the document under the finger that pressed it, which is also why
        // a failed page turn below keeps the rows it already had.
        if (_loading)
        {
            return;
        }

        var target = Math.Clamp(page, 1, TotalPages);

        // Also the whole of what makes a click on an unavailable button inert: at either end the
        // clamped target is the page already on screen.
        if (target == _page)
        {
            return;
        }

        // All three kept so a failed fetch can put them back. The result as well as the number,
        // because the retreat below turns a second page and has to be able to undo both of them
        // together — and the panel with them, because the retreat's route passes through an empty
        // answer that closes it on the way.
        var previous = _page;
        var previousResult = _result;
        var previousPanel = CapturePanel();

        // A pager button was pressed, so the pager stays until a search or a sort replaces the
        // result — including through a retreat that lands on a single-page answer.
        _keepPager = true;

        _page = target;

        // keepResult: the pressed button must survive the failure. The rest of the component
        // never removes a control the user just used, and the pager is the only pressable thing in
        // it that is rendered conditionally — so a page turn that cleared the rows would take
        // Forrige and Neste out of the document in the same render that reports the error, drop
        // focus to <body>, and leave a keyboard user restarting from the top of the host's page.
        if (!await FetchAsync(_executedSearch, keepResult: true))
        {
            // Nothing arrived, so the state has to keep describing what did — and what did is
            // still on screen. Same invariant the sort rollback protects.
            _page = previous;

            return;
        }

        await RetreatFromEmptyPageAsync(previous, previousResult, previousPanel);
    }

    /// <summary>
    /// Step back to a page that has rows, when the page just fetched turned out not to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clamp in <see cref="GoToPageAsync"/> measures the target against the count the
    /// <em>previous</em> answer carried, so it can only ever ask for a page that existed when that
    /// answer was written. Two routes lead past it: the index shrinks between the two requests, and
    /// the API answers an out-of-range page with 404 — which
    /// <see cref="IMuninExplorerClient.SearchVariablesAsync"/> reports as an empty page rather than
    /// throwing, so no rollback runs.
    /// </para>
    /// <para>
    /// Left alone, either one strands the reader: the status line would say "Ingen variabler passet
    /// søket" over a search that matched hundreds, with no rows to show and nothing but a fresh
    /// search to get back from. So the component takes itself back to a page that exists — the last
    /// one the new answer admits to, or page 1, which is the one page that can never be out of
    /// range. One step only: a second empty answer is not retreated from again, so the reader is
    /// left on that page with the pager still under their finger rather than walking backwards
    /// through the result a page at a time.
    /// </para>
    /// <para>
    /// And its own fetch is checked like every other one. <paramref name="previous"/>,
    /// <paramref name="previousResult"/> and <paramref name="previousPanel"/> are the page turn's
    /// starting point — a page that had rows on it, and whatever was open among them — so a retreat
    /// that fails puts the reader back where they pressed the button instead of leaving
    /// <c>_page</c> naming one page while the empty answer for another is still on screen. That
    /// pairing is what would otherwise report "Ingen variabler passet søket" over a search that
    /// matched hundreds and take the pager with it, which is the exact state this method exists to
    /// prevent. The panel is part of the same undo: the empty answer closed it on the way past, and
    /// a rollback that put the rows back without it would leave the reader looking at the row they
    /// opened, shut, with their URL no longer naming it.
    /// </para>
    /// </remarks>
    private async Task RetreatFromEmptyPageAsync(
        int previous, Page<VariableSummary>? previousResult, PanelState previousPanel)
    {
        if (_page == 1 || _result is not { Items.Count: 0 })
        {
            return;
        }

        // TotalPages reads the answer that just arrived, so this is the new count and not the stale
        // one the clamp trusted. A server still claiming the page exists after sending nothing has
        // told us nothing usable, so page 1 is the only safe answer left.
        var last = TotalCount > 0 ? TotalPages : 1;
        _page = last < _page ? last : 1;

        if (await FetchAsync(_executedSearch, keepResult: true))
        {
            return;
        }

        // Nothing arrived, so — exactly as on the first fetch — the state has to go back to
        // describing the last answer that did. keepResult held on to the empty page that started
        // the retreat, which is the one result that must not be the one left on screen.
        _page = previous;
        _result = previousResult;

        // After the rows, so the row the panel is drawn inside is back before the panel is.
        await RestorePanelAsync(previousPanel);
    }

    /// <summary>What is open in the panel and what was fetched into it.</summary>
    private readonly record struct PanelState(Guid? Id, VariableDetail? Detail, string? Error, SourceState Source);

    /// <summary>What is open in the kilde or datasamling panel inside it, and what was fetched.</summary>
    private readonly record struct SourceState(
        SourceKind? Kind, KildeDetail? Kilde, DatasamlingDetail? Datasamling, string? Error);

    private PanelState CapturePanel() => new(_selectedId, _detail, _detailError, CaptureSource());

    private SourceState CaptureSource() => new(_sourceKind, _kilde, _datasamling, _sourceError);

    /// <summary>
    /// Reopen a panel that a fetch closed on its way through, when that fetch then failed.
    /// </summary>
    /// <remarks>
    /// The fetched detail goes back rather than being asked for again, for the reason the previous
    /// result does: it is the answer that described these very rows, and putting a second request
    /// in the way of a rollback would let one failure turn into two. The exception is a panel
    /// captured while its own fetch was still running — it has no answer to put back, so that one
    /// is fetched, and the host waits for that fetch before being told: what is raised is the
    /// selection as it stands afterwards, which on a slow re-fetch the reader may have moved.
    /// The host is told at all because it was told null on the way in.
    /// </remarks>
    private async Task RestorePanelAsync(PanelState panel)
    {
        if (panel.Id is not { } id || _selectedId == id)
        {
            return;
        }

        _selectedId = id;
        _detail = panel.Detail;
        _detailError = panel.Error;

        // A new owner of the panel: whatever was in flight when it closed must not land in the one
        // just put back.
        _detailGeneration++;
        _detailLoading = false;

        if (panel.Detail is null && panel.Error is null)
        {
            await LoadDetailAsync(id);
        }

        // After the detail, for the reason the detail comes after the rows: the owner panel is
        // drawn inside the variable's, and LoadDetailAsync clears it on its way through.
        await RestoreSourceAsync(panel.Source, id);

        // _selectedId rather than id, for the reason ToggleDetailAsync gives: the fetch above
        // yields with the rows already back on screen and clickable, so another row may have been
        // opened while it ran, and what the host is told has to be what is open.
        await RaiseAsync(SelectedVariableIdChanged, _selectedId);
    }

    /// <summary>
    /// Put the kilde or datasamling panel back alongside the variable panel it hung inside.
    /// </summary>
    /// <remarks>
    /// Same reasoning as <see cref="RestorePanelAsync"/>, one level down: the rollback exists so a
    /// failed page turn does not leave the reader on the row they had opened, shut — and a reader
    /// who had opened the kilde inside it was two presses in, not one. The fetched payload goes
    /// back rather than being asked for again, except when it had not arrived yet, which is the one
    /// case with nothing to put back.
    /// <para>
    /// Guarded on the selection still being the restored row: the detail above may have been
    /// re-fetched, and that yields with the rows clickable, so the reader can have opened another
    /// variable in the meantime. Restoring an owner into that one would name the wrong kilde under
    /// the wrong variable.
    /// </para>
    /// </remarks>
    private async Task RestoreSourceAsync(SourceState source, Guid id)
    {
        if (source.Kind is not { } kind || _selectedId != id)
        {
            return;
        }

        _sourceKind = kind;
        _kilde = source.Kilde;
        _datasamling = source.Datasamling;
        _sourceError = source.Error;

        // A new owner of the panel, for the reason RestorePanelAsync bumps the detail's.
        _sourceGeneration++;
        _sourceLoading = false;

        if (source.Kilde is not null || source.Datasamling is not null || source.Error is not null)
        {
            return;
        }

        // Nothing had arrived when it closed, so it has to be asked for again — and only the
        // restored detail can say which id to ask for. A detail that came back without one is a
        // panel with nothing to open, so the owner closes rather than hanging empty.
        if (_detail is { } detail && SourceIdOf(detail, kind) is { } sourceId)
        {
            await LoadSourceAsync(kind, sourceId);
        }
        else
        {
            ClearSource();
        }
    }

    /// <summary>
    /// Tell the host what was searched for, so it can reflect it in its own URL.
    /// </summary>
    /// <remarks>
    /// Raised whether or not the fetch succeeded, which is what <see cref="SearchChanged"/>
    /// documents: a host whose URL kept the previous query after a failed search would hand out a
    /// link that reloads into a different search than the box on screen is showing.
    /// </remarks>
    private Task NotifySearchChangedAsync() => RaiseAsync(SearchChanged, _search);

    /// <summary>
    /// Hand a value to one of the host's callbacks without letting the host's own failure out.
    /// </summary>
    /// <remarks>
    /// Shared by <see cref="SearchChanged"/> and <see cref="FilterChanged"/>, because what has to be
    /// survived is the same for both: the handler is the host's, and what it most often does is
    /// rewrite a URL.
    /// </remarks>
    private static async Task RaiseAsync<TValue>(EventCallback<TValue> callback, TValue value)
    {
        if (!callback.HasDelegate)
        {
            return;
        }

        try
        {
            await callback.InvokeAsync(value);
        }
        catch (NavigationException)
        {
            // A host that navigates from its handler. During static SSR that is signalled by this
            // exception and the framework turns it into the redirect, so swallowing it would drop
            // the navigation on the floor.
            throw;
        }
        catch (Exception)
        {
            // The host's handler threw, and a NavigationManager call or a CMS URL rewrite is
            // exactly the kind that does. Left unhandled it would propagate out of Blazor's event
            // dispatch — and this same path runs from OnInitializedAsync, so during initial render
            // too. In helsedata's legacy Blazor Server host inside Optimizely that tears down the
            // circuit for the whole CMS page, not just this component.
            //
            // Nothing is said to the reader on top of what the search already reported for itself,
            // success or failure. What broke here is the host's own URL, which is the host's bug to
            // find in the host's logs — and reporting it as "Kunne ikke hente variabler" would
            // blame the API for a call the API was never part of.
        }
    }

    /// <summary>
    /// Fetch <paramref name="search"/> at the current page and ordering, and settle what the new
    /// rows mean for the open detail panel. True when the fetch succeeded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The panel is settled here rather than at the five call sites, which is what makes "the
    /// selection is always a row on screen" one rule instead of five. It is outside the fetch's own
    /// try/catch on purpose: the host's callback runs in it, and a host that navigates from its
    /// handler signals that with an exception the catch would otherwise swallow and report as a
    /// failed search.
    /// </para>
    /// <para>
    /// Settled after a failure too, not only after an answer. A search or a sort that fails clears
    /// the rows, so the panel leaves the document with them — and a selection left set behind it is
    /// the invisible, unremovable state <see cref="DropSelectionIfGoneAsync"/> exists to prevent,
    /// with the host's URL still naming a variable the page is not showing. A page turn fails with
    /// <paramref name="keepResult"/>, so its rows and its panel are both still there and the check
    /// finds nothing to drop.
    /// </para>
    /// </remarks>
    private async Task<bool> FetchAsync(string? search, bool keepResult = false)
    {
        var fetched = await FetchRowsAsync(search, keepResult);

        await DropSelectionIfGoneAsync();

        return fetched;
    }

    /// <summary>Fetch <paramref name="search"/> at the current page and ordering. True when it succeeded.</summary>
    /// <remarks>
    /// <para>
    /// The search is a parameter rather than read from <c>_search</c>, because the two callers do
    /// not mean the same thing by it: searching means the live contents of the box, sorting means
    /// the text the visible rows actually came from.
    /// </para>
    /// <para>
    /// <paramref name="keepResult"/> keeps the rows already on screen when the call fails,
    /// which is what a page turn wants and a search does not. A search that failed has no result
    /// to describe — the rows on screen came from a different query, and leaving them there under
    /// the new search's error message would say they answered it. A page turn's rows came from the
    /// query that is still on screen, so they stay, and with them the pager button the reader is
    /// standing on.
    /// </para>
    /// </remarks>
    private async Task<bool> FetchRowsAsync(string? search, bool keepResult = false)
    {
        _loading = true;
        _error = null;
        StateHasChanged();

        try
        {
            _result = await Client.SearchVariablesAsync(
                search,
                _filter,
                page: _page,
                pageSize: ClampedPageSize,
                sort: _sort,
                direction: _direction);
            _executedSearch = Trimmed(search);

            // The page we are on is the page that arrived, not the page that was asked for. A
            // server that clamps page 12 to page 8 and says so has answered truthfully, and
            // ResultPage already counts the row range from its answer — leaving _page at 12 would
            // caption those rows "Side 12 av 8" and, worse, keep Neste enabled against a number
            // the server disowned, so every further press would walk the position further from the
            // rows without ever moving them. One page number for the caption, the two buttons and
            // the range, taken from the same place.
            _page = ResultPage;

            return true;
        }
        catch (Exception)
        {
            // Say what the reader can do about it; the detail belongs in the host's logs,
            // not on the page.
            if (!keepResult)
            {
                _result = null;
            }

            _error = T.Error;

            return false;
        }
        finally
        {
            _loading = false;
        }
    }

    private static string? Trimmed(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? Period(VariableSummary v) => Period(v.DataFrom, v.DataTo);

    /// <summary>
    /// The years a variable has data for, as the cards and the detail panel both write it.
    /// </summary>
    /// <remarks>
    /// Shared so a row and the panel opened from it cannot word the same period differently — the
    /// two dates come from different payloads, but the sentence they are written into is one.
    /// </remarks>
    private static string? Period(DateTimeOffset? dataFrom, DateTimeOffset? dataTo)
    {
        var from = dataFrom?.Year.ToString();
        var to = dataTo?.Year.ToString();
        return (from, to) switch
        {
            (null, null) => null,
            (not null, null) => $"{from}–",
            (null, not null) => $"–{to}",
            _ => from == to ? from! : $"{from}–{to}"
        };
    }
}
