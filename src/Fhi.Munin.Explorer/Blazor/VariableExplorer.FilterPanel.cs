using System.Globalization;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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
    /// <para>
    /// <c>Body</c> is a facet whose control is not a list of values — the dataperiode's date fields,
    /// which hold no <see cref="FacetValue"/> and so survive neither other shape. Such a facet has
    /// to report <c>ChosenCount</c> itself, or it would say nothing in the summary while narrowing.
    /// (Fhi.Metadata-uidue)
    /// </para>
    /// </remarks>
    private sealed record FacetGroup(
        string Key,
        string Label,
        bool OpenByDefault,
        IReadOnlyList<FacetValue> Values,
        string? EmptyText = null,
        RenderFragment? Body = null,
        int? ChosenCount = null)
    {
        /// <summary>How many values in this facet are selected, counting nested ones.</summary>
        public int SelectedCount => ChosenCount ?? Selected(Values);

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
            //
            // Datakategori third. Runa puts it FIRST, and that slot is not available here: the two
            // above it are in helsedata's order on purpose, and moving them would trade a reason
            // this panel has for one it is copying. Third is as near Runa's placement as that
            // leaves, and it keeps datakategori above the facets it is coarser than — a reader
            // narrowing by kind of data does it before picking variabelgrupper.
            //
            // Dataperiode after datatype and before helsefaglig kodeverk, which IS Runa's own slot
            // for it. (Fhi.Metadata-uidue)
            List<FacetGroup?> groups =
            [
                KildeTypeGroup(facets),
                KildeGroup(facets),
                DataCategoryGroup(facets),
                VariabelgruppeGroup(facets),
                SavedFilterGroup(facets),
                DataTypeGroup(facets),
                DataPeriodGroup(facets),
                HelsefagligKodeverkGroup(facets),
                AdministrativtKodeverkGroup(facets),
                InstrumentGroup(facets),
                OtherGroup(facets)
            ];

            // A facet the API returned nothing for is left out rather than drawn as an empty
            // disclosure — except where the emptiness is itself the message, or where the facet's
            // control is not a list of values at all. The dataperiode is the latter: it holds no
            // FacetValue and would be dropped here as empty, though it has two date fields to draw.
            return
            [
                .. groups
                    .OfType<FacetGroup>()
                    .Where(group =>
                        group.Values.Count > 0 || group.EmptyText is not null || group.Body is not null)
            ];
        }
    }

    /// <summary>The datakategori facet — the EHDS tokens a variable's datasamling carries.</summary>
    /// <remarks>
    /// An ordinary multi-select facet: the values are a flat list, ticking one adds it to
    /// <see cref="VariableFilter.Categories"/>, and two ticked leave the variables matching either.
    /// The words come from the catalogue's vocabulary rather than from this package — see
    /// <see cref="_vocabulary"/> for why a table here would be wrong.
    /// </remarks>
    private FacetGroup DataCategoryGroup(FilterOptions facets) =>
        new("datakategori", T.FacetDataCategory, OpenByDefault: false,
            [.. facets.DataCategories.Select(DataCategoryValue)]);

    private FacetValue DataCategoryValue(DataCategoryFacet category) =>
        new($"datakategori:{category.Value}",
            CategoryWord(category.Value),
            Counted(category.Count),
            // Ordinal, like every other string facet here, because that is what ToggleAsync removes
            // with: a case-insensitive mark over a case-sensitive toggle draws a token as chosen
            // and then appends a duplicate when it is pressed.
            _filter.Categories.Contains(category.Value),
            () => ToggleAsync(_filter.Categories, category.Value,
                              values => _filter with { Categories = values }),
            []);

    /// <summary>
    /// The catalogue's word for one EHDS token, or the token itself where there is none.
    /// </summary>
    /// <remarks>
    /// The miss is shown rather than hidden, which is the rule <see cref="CatalogueProperties.Word"/>
    /// states: a facet drawing nothing for a token it cannot name would silently offer fewer
    /// choices than the catalogue has. A token is ugly and honest.
    /// </remarks>
    private string CategoryWord(string value) =>
        _vocabulary.TryGetValue(DataCategoryKey, out var entry)
        && CatalogueProperties.Word(entry, value, Reader) is { } word
            ? word.Label
            : value;

    /// <summary>
    /// The dataperiode facet — two date fields rather than a list of values.
    /// </summary>
    /// <remarks>
    /// The <c>Body</c> shape. Bounds come from the API's range where it reports one; without one
    /// the fields are unbounded, and drawn at all only when a date is already set — so the control
    /// that applied a filter cannot vanish under it. (Fhi.Metadata-yxhv1)
    /// </remarks>
    private FacetGroup? DataPeriodGroup(FilterOptions facets)
    {
        // One per bound the reader has set, so a folded dataperiode says it is narrowing the way
        // every other facet does. Without it the summary reads plain "Dataperiode" over an active
        // date filter — the facet holds no values to count.
        var chosen = (_filter.DataFrom is null ? 0 : 1) + (_filter.DataTo is null ? 0 : 1);
        var range = facets.DateRange;
        var reported = range is { } r && (r.Min is not null || r.Max is not null);

        // Drawn when the API reports a range, and drawn regardless whenever the reader has a date
        // set. A date filter matching nothing is exactly when the API stops reporting a range, so
        // dropping the facet then takes away the only control that can undo it. (Fhi.Metadata-yxhv1)
        if (!reported && chosen == 0)
        {
            return null;
        }

        return new FacetGroup("dataperiode", T.FieldDataPeriod, OpenByDefault: false, [],
                              Body: DateFields(range ?? new DateInterval()), ChosenCount: chosen);
    }

    /// <summary>The from and to fields, each bounded by the range and by the other.</summary>
    /// <remarks>
    /// Labelled and bound one at a time rather than as a range control: Stiler has no date-range
    /// widget, and the two native inputs are elements every stylesheet already draws — the same
    /// argument the panel's <c>&lt;details&gt;</c> and bare <c>&lt;ul&gt;</c> are built on. No class
    /// name is invented here; the labels wear <c>form-element__label</c>, which this panel already
    /// uses and which is verified against the host.
    /// </remarks>
    private RenderFragment DateFields(DateInterval range) => builder =>
    {
        DateField(builder, 0, DateFromId, T.FacetDateFrom, _filter.DataFrom,
                  Bound(range.Min), _filter.DataTo ?? Bound(range.Max),
                  value => ApplyFilterAsync(_filter with { DataFrom = value }));

        DateField(builder, 100, DateToId, T.FacetDateTo, _filter.DataTo,
                  _filter.DataFrom ?? Bound(range.Min), Bound(range.Max),
                  value => ApplyFilterAsync(_filter with { DataTo = value }));
    };

    /// <summary>
    /// One date field: a label, and an input bounded at both ends.
    /// </summary>
    private void DateField(
        RenderTreeBuilder builder, int seq, string id, string label, DateOnly? value,
        DateOnly? min, DateOnly? max, Func<DateOnly?, Task> set)
    {
        builder.OpenElement(seq, "label");
        builder.AddAttribute(seq + 1, "class", "form-element__label");
        builder.AddAttribute(seq + 2, "for", id);
        builder.AddContent(seq + 3, label);
        builder.CloseElement();

        builder.OpenElement(seq + 10, "input");
        builder.AddAttribute(seq + 11, "id", id);
        builder.AddAttribute(seq + 12, "type", "date");
        builder.AddAttribute(seq + 13, "value", Iso(value));
        builder.AddAttribute(seq + 14, "min", Iso(min));
        builder.AddAttribute(seq + 15, "max", Iso(max));

        // onchange, not oninput: a partly typed date is a date the browser reports as it is being
        // typed, and every keystroke would be a search. The same reason the search box binds on
        // change.
        //
        // The awaiting binder overload rather than a void one discarding the task. A dropped task
        // is a fetch whose failure nothing observes — the rollback ApplyFilterAsync does on a failed
        // search would run with no one waiting on it, and the exception would surface as an
        // unobserved task rather than in the panel's own alert region.
        builder.AddAttribute(seq + 16, "onchange",
            EventCallback.Factory.CreateBinder<string?>(this, raw =>
            {
                var typed = Parse(raw);

                return Within(typed, min, max) ? set(typed) : Task.CompletedTask;
            }, Iso(value)));

        builder.CloseElement();
    }

    /// <summary>Whether a typed date is inside the bounds the field itself advertises.</summary>
    /// <remarks>
    /// A date input reports a complete value once all three segments hold digits, so a half-typed
    /// year arrives as 0002 and would otherwise be applied. (Fhi.Metadata-yxhv1)
    /// </remarks>
    private static bool Within(DateOnly? value, DateOnly? min, DateOnly? max) =>
        value is not { } date || ((min is not { } lo || date >= lo) && (max is not { } hi || date <= hi));

    private string DateFromId => $"munin-explorer-date-from-{_instance}";

    private string DateToId => $"munin-explorer-date-to-{_instance}";

    /// <summary>
    /// A reported bound as the date it names, without asking what time zone anyone is in.
    /// </summary>
    /// <remarks>
    /// <see cref="DateTimeOffset.Date"/> is the date as the value itself writes it, so
    /// <c>2020-01-01T00:00:00+02:00</c> is 1 January whoever reads it. <c>UtcDateTime.Date</c> would
    /// make it 31 December, and <c>LocalDateTime.Date</c> would hand the answer to whichever machine
    /// the code runs on — so CI and a Norwegian laptop would disagree and neither would be wrong.
    /// The filter is a <see cref="DateOnly"/> for the same reason; see the remarks on
    /// <see cref="VariableFilter.DataFrom"/>, which prescribes exactly this conversion.
    /// </remarks>
    private static DateOnly? Bound(DateTimeOffset? instant) =>
        instant is { } value ? DateOnly.FromDateTime(value.Date) : null;

    private static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly? Parse(string? raw) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>The kildetype facet — one value each, and only one of them can be chosen.</summary>
    private FacetGroup KildeTypeGroup(FilterOptions facets) =>
        new("kildetype", T.FacetKildeType, OpenByDefault: true, [.. facets.KildeTyper.Select(KildeTypeValue)]);

    private FacetValue KildeTypeValue(KildetypeFacet type) =>
        new($"kildetype:{type.Value}",
            // The facet's own displayName is the raw enum name (SentraltHelseregister), so the
            // prose comes from the component's own translations and falls back to what the API said.
            T.KildeTypeLabel(type.Value, type.DisplayName),
            Counted(type.Count),
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
            Counted(kilde.Count),
            _filter.KildeIds.Contains(kilde.Id),
            () => ToggleAsync(_filter.KildeIds, kilde.Id, ids => _filter with { KildeIds = ids }),
            DelkildeChildren(kilde.Id, delkilderByKilde));

    private IReadOnlyList<FacetValue> DelkildeChildren(Guid kildeId, ILookup<Guid, DelkildeFacet> delkilderByKilde) =>
        Tree(delkilderByKilde[kildeId].Select(d => new TreeNode(d.Id, d.ParentDelkildeId, d.Name, d.Count)),
             "delkilde:",
             IsDelkildeChosen,
             ToggleDelkilde, Counted);

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
                 ToggleGruppe, Counted),
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
                 ToggleSavedFilter, Counted));

    private bool IsSavedFilterChosen(Guid id) => _filter.FilterIds.Contains(id);

    private Func<Task> ToggleSavedFilter(Guid id) =>
        () => ToggleAsync(_filter.FilterIds, id, ids => _filter with { FilterIds = ids });

    private FacetGroup DataTypeGroup(FilterOptions facets) =>
        new("datatype", T.FacetDataType, OpenByDefault: false, [.. facets.DataTypes.Select(DataTypeValue)]);

    private FacetValue DataTypeValue(DataTypeFacet dataType) =>
        new($"datatype:{dataType.Value}",
            // The API returns the code with no label at all, so the prose is the component's own.
            T.DataTypeLabel(dataType.Value),
            Counted(dataType.Count),
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
            Counted(kodeverk.Count),
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
            Counted(kodeverk.Count),
            _filter.AdministrativtKodeverk.Contains(kodeverk.Oid),
            () => ToggleAsync(_filter.AdministrativtKodeverk, kodeverk.Oid,
                              values => _filter with { AdministrativtKodeverk = values }),
            []);

    private FacetGroup InstrumentGroup(FilterOptions facets) =>
        new("instrument", T.FacetInstrument, OpenByDefault: false, [.. facets.Instruments.Select(InstrumentValue)]);

    private FacetValue InstrumentValue(InstrumentFacet instrument) =>
        new($"instrument:{instrument.Id}",
            string.IsNullOrWhiteSpace(instrument.Name) ? instrument.Code : instrument.Name,
            Counted(instrument.Count),
            _filter.InstrumentIds.Contains(instrument.Id),
            () => ToggleAsync(_filter.InstrumentIds, instrument.Id, ids => _filter with { InstrumentIds = ids }),
            []);

    /// <summary>The two filters that are a yes/no rather than a choice of values.</summary>
    private FacetGroup OtherGroup(FilterOptions facets) =>
        new("other",
            T.FacetOther,
            OpenByDefault: false,
            [
                new FacetValue("has-kildekodeverk", T.HasKildekodeverk, Counted(facets.KildeKodeverkCount),
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
        Func<Guid, Func<Task>> toggle,
        Func<int, int?> count)
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

            return new FacetValue($"{keyPrefix}{node.Id}", node.Label, count(node.Count), selected(node.Id), toggle(node.Id), children);
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
        var previousPage = _page;
        var previousKeepPager = _keepPager;

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
            // invariant the sort rollback protects. The page with them: those rows are page 7 of the
            // old selection, and leaving _page at 1 would report a page the reader is not on and
            // take it out of the host's URL over a narrowing that never happened.
            _filter = previous;
            _page = previousPage;
            _keepPager = previousKeepPager;
        }

        // _filter and not next: what the host is told is what is in force, rolled back or not.
        await RaiseAsync(FilterChanged, _filter);

        // Narrowing renumbers the pages, so a host mirroring this into a URL has to drop the page
        // it was holding. Same rule as the filter: whatever is in force, rolled back or not.
        await NotifyPageChangedAsync();
    }

    /// <summary>
    /// The catalogue's own words for the EHDS tokens the datakategori facet is made of.
    /// </summary>
    /// <remarks>
    /// <see cref="DataCategoryFacet"/> carries a CURIE and a count and no label, so without this the
    /// facet would read <c>ehds-cat:population-health-surveys</c> down the panel. Transcribing the
    /// vocabulary into <see cref="Texts"/> is the other way to get words, and is the one the note
    /// above <c>Texts.FacetCategory</c> forbids: a table copied here is right on the day it is
    /// written and drifts from then on. Kelda resolves the same vocabulary the same way.
    /// <para>
    /// Empty until the fetch lands, and empty for good if it fails — which costs the choices their
    /// words and nothing else, exactly as it does in Kelda. The facet still filters.
    /// </para>
    /// </remarks>
    private IReadOnlyDictionary<string, PropertyMetadataEntry> _vocabulary =
        new Dictionary<string, PropertyMetadataEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the vocabulary has been asked for, however that turned out.</summary>
    private bool _vocabularyAsked;

    /// <summary>The property key the datakategori tokens are defined under.</summary>
    private const string DataCategoryKey = "healthCategory";

    /// <summary>
    /// Fetch the vocabulary once, and only for a panel that has datakategorier to name.
    /// </summary>
    /// <remarks>
    /// Lazy rather than fetched on mount: an API that predates the facet returns no datakategorier
    /// at all (see <see cref="FilterOptions.DataCategories"/>), and a request whose answer nothing
    /// on screen could use is one more call against a rate limit this component already shares with
    /// the search beside it. Asked at most once per component, failure included — a vocabulary that
    /// could not be had will not be had by asking again on every keystroke.
    /// </remarks>
    private async Task EnsureCategoryWordsAsync()
    {
        if (_vocabularyAsked || _facets is not { DataCategories.Count: > 0 })
        {
            return;
        }

        _vocabularyAsked = true;

        try
        {
            var entries = await Client.GetKildePropertyMetadataAsync();

            _vocabulary = entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Deliberately silent, and deliberately not surfaced beside the facet error. The panel
            // is not broken without it: every choice still filters, and the reader sees the token
            // rather than the word. A second failure message for a degraded label would be louder
            // than what it reports.
        }
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
    /// <summary>Whether the panel is drawing controls the last non-empty answer supplied.</summary>
    /// <remarks>
    /// True only while a selection returns nothing. The controls are the reader's own; the counts
    /// beside them are not ours to state, so they are dropped. (Fhi.Metadata-v2bgr)
    /// </remarks>
    private bool _facetsRetained;

    /// <summary>A count, or none while the counts on screen would describe another moment.</summary>
    private int? Counted(int count) => _facetsRetained ? null : count;

    /// <summary>Whether an answer offers nothing to choose from, in any facet.</summary>
    private static bool OffersNothing(FilterOptions facets) =>
        facets.KildeTyper.Count == 0 && facets.Kilder.Count == 0 && facets.Delkilder.Count == 0
        && facets.Variabelgrupper.Count == 0 && facets.Filters.Count == 0 && facets.DataTypes.Count == 0
        && facets.HelsefagligKodeverk.Count == 0 && facets.AdministrativtKodeverk.Count == 0
        && facets.Instruments.Count == 0 && facets.DataCategories.Count == 0;

    /// <summary>
    /// The answer to draw the panel from: the fresh one, unless it would leave the reader stranded.
    /// </summary>
    /// <remarks>
    /// A selection matching nothing makes the API report nothing for every facet, chosen values
    /// included, so storing it would remove the only way to undo the choice that emptied the list.
    /// (Fhi.Metadata-v2bgr)
    /// </remarks>
    private async Task<FilterOptions> RetainedAsync(FilterOptions fresh, string? language)
    {
        if (_filter.IsEmpty || !OffersNothing(fresh))
        {
            _facetsRetained = false;

            return fresh;
        }

        _facetsRetained = true;

        if (_facets is not null)
        {
            return _facets;
        }

        // Nothing to keep: the reader arrived on a link that already matches nothing, so there was
        // never a populated answer on screen. Ask what the catalogue holds at all — without it the
        // panel has no way to show what they arrived with, which is the state the link put them in.
        try
        {
            return await Client.GetFiltersAsync(_executedSearch, VariableFilter.None, language);
        }
        catch (Exception)
        {
            // Its own failure is not the facets failing: the first answer arrived, it simply had
            // nothing in it. Reported as the empty panel it is rather than as an error.
            _facetsRetained = false;

            return fresh;
        }
    }

    private async Task FetchFacetsAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            // The API's own spelling of the resolved language rather than the host's raw token or
            // the tag we render with. A host sending "en-GB" gets English words everywhere else,
            // and a facet panel that asked the API for a tag it does not know would be the one
            // Norwegian block on an otherwise English page. Norwegian goes out as "nb" and not our
            // "no" for the same reason in reverse: "no" has no parent culture the API's request
            // localization can fall back from, so it would silently take the API's default.
            var language = ReaderLanguage.ForApi(Language);

            _facets = await RetainedAsync(
                await Client.GetFiltersAsync(_executedSearch, _filter, language), language);
            _facetError = null;
            _retryFacetsEnabled = false;

            // After the facets, because whether it is worth asking at all depends on what they
            // hold. Its own failure is swallowed inside — the counts arriving is what this try
            // block reports on, and a missing label must not read as a missing facet.
            await EnsureCategoryWordsAsync();
        }
        catch (MuninExplorerRateLimitedException)
        {
            // This refresh goes out alongside every search, so a throttled reader meets this panel
            // and the result list in the same render. "The counts may be out of date" beside "you
            // have made too many requests" would have the two regions disagree about what happened,
            // and only one of them would be telling the reader what to do about it.
            _facetError = T.RateLimitError;

            // Offered no more here than beside the rows, and for the same reason: waiting is the
            // remedy, so a button saying otherwise would contradict the sentence it sits under.
            _retryFacetsEnabled = false;
        }
        catch (Exception)
        {
            _facetError = T.FilterError;
            _retryFacetsShown = true;
            _retryFacetsEnabled = true;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Ask for the counts again after a refresh that failed.</summary>
    /// <remarks>
    /// Refreshes the counts and nothing else. The rows are the right rows — that is exactly what
    /// makes this a failure of its own — so a handler shared with the row retry would re-fetch a
    /// list nobody said was wrong, and clear this message with the answer to a different question.
    /// <para>
    /// No arguments to capture, unlike <see cref="RetryRowsAsync"/>: the counts describe whatever
    /// is on screen, and <c>_executedSearch</c> and <c>_filter</c> are still describing it.
    /// </para>
    /// </remarks>
    private async Task RetryFacetsAsync()
    {
        // Inert rather than absent once there is nothing left to retry, the same as the clear
        // button above it — and dropped while a fetch is in flight, the same as every other press.
        if (_loading || !_retryFacetsEnabled)
        {
            return;
        }

        await FetchFacetsAsync();
    }
}
