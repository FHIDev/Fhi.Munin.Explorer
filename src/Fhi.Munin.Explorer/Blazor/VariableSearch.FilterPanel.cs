using System.Globalization;
using System.Text;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The facet sidebar: what can be narrowed, and what narrowing it costs.</summary>
public partial class VariableSearch
{
    /// <summary>One facet, as the panel draws it: a disclosure holding a list of values.</summary>
    /// <remarks>
    /// A null <c>EmptyText</c> drops an empty facet. A <c>Body</c> facet and a <c>Searchable</c> one
    /// report <c>Chosen</c> themselves, so a value out of sight still counts. (Fhi.Metadata-uidue)
    /// </remarks>
    private sealed record FacetGroup(
        string Key,
        string Label,
        bool OpenByDefault,
        IReadOnlyList<FacetValue> Values,
        string? EmptyText = null,
        RenderFragment? Body = null,
        IReadOnlyList<FacetValue>? Chosen = null,
        bool Searchable = false,
        bool NameInChips = false,
        int? ValuesBeforeSearch = null)
    {
        /// <summary>How many top-level values the facet holds before its own search narrows it.</summary>
        /// <remarks>The length a lifted cap is judged against: <see cref="FacetLimits.StillExpanded"/>.</remarks>
        public int TotalValues => ValuesBeforeSearch ?? Values.Count;

        /// <summary>What is chosen in this facet: the summary's count, and the row of chips.</summary>
        /// <remarks>
        /// One projection for both, so the two can never describe different selections. (Fhi.Metadata-l9l2n.68)
        /// </remarks>
        public IReadOnlyList<FacetValue> ChosenValues => Chosen ?? [.. Selected(Values)];

        /// <summary>How many values in this facet are selected, counting nested ones.</summary>
        public int SelectedCount => ChosenValues.Count;

        /// <summary>Every selected value in the tree, parents before the values under them.</summary>
        private static IEnumerable<FacetValue> Selected(IReadOnlyList<FacetValue> values)
        {
            foreach (var value in values)
            {
                if (value.Selected)
                {
                    yield return value;
                }

                foreach (var nested in Selected(value.Children))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>One facet value and its children; a null <c>Toggle</c> is a container, not a filter.</summary>
    /// <remarks>
    /// <c>GroupHeading</c> counts children (Fhi.Metadata-adog5); a null <c>Language</c> means "do not
    /// mark". <c>Badge</c> is a word apart from <c>Icons</c>, so hiding decoration hides no fact.
    /// </remarks>
    private sealed record FacetValue(
        string Key,
        string Label,
        string? Language,
        int? Count,
        bool Selected,
        Func<Task>? Toggle,
        IReadOnlyList<FacetValue> Children,
        bool GroupHeading = false,
        IReadOnlyList<NodeIcon>? Icons = null,
        string? Badge = null,
        int ChosenBelow = 0);

    /// <summary>A node on the way to becoming a <see cref="FacetValue"/> tree.</summary>
    /// <remarks>The flat parented facets all become trees through <see cref="Tree"/>, so the rule lives once.</remarks>
    private sealed record TreeNode(Guid Id, Guid? ParentId, string Label, string? Language, int Count);

    /// <summary>A node whose label came out of the catalogue, carrying that label's language with it.</summary>
    private static TreeNode Node(Guid id, Guid? parentId, (string Text, string? Language) label, int count) =>
        new(id, parentId, label.Text, label.Language, count);

    /// <summary>A facet value's words for a thing the catalogue may have left unnamed, and their language.</summary>
    /// <remarks>
    /// Only the catalogue's own name is Norwegian; marking a code or this package's prose would read it
    /// in a Norwegian voice, which is <c>lang</c> applied backwards (WCAG 3.1.2).
    /// </remarks>
    private (string Text, string? Language) CatalogueName(string? name, string? code)
    {
        var (text, norwegian) = T.Named(name, code);

        return (text, CatalogueProperties.Foreign(norwegian, Reader));
    }

    /// <summary>The facets on screen, in the order they are drawn.</summary>
    /// <remarks>
    /// Built from the last answer rather than cached, so a facet's selected state and its count can
    /// never describe two different moments.
    /// </remarks>
    private IReadOnlyList<FacetGroup> FacetGroups
    {
        get
        {
            if (_facets is not { } facets)
            {
                return [];
            }

            // Kildetype and kilde in helsedata's order; datakategori third, as near Runa's first slot as that
            // leaves; dataperiode in Runa's own slot after datatype. The rest follow Munin. (Fhi.Metadata-uidue)
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

            // An empty facet is dropped unless its emptiness is the message, or its control is not a list of
            // values at all — the dataperiode holds no FacetValue but has two date fields to draw.
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
    /// An ordinary multi-select facet. The words come from the catalogue's vocabulary rather than
    /// from this package — see <see cref="_vocabulary"/>.
    /// </remarks>
    private FacetGroup DataCategoryGroup(FilterOptions facets) =>
        new("datakategori", T.FacetDataCategory, OpenByDefault: false,
            [.. facets.DataCategories.Select(DataCategoryValue)]);

    private FacetValue DataCategoryValue(DataCategoryFacet category)
    {
        var (label, language) = CategoryWord(category.Value);

        return new($"datakategori:{category.Value}",
            label,
            language,
            Counted(category.Count),
            // Ordinal, like every other string facet here, because that is what ToggleAsync removes
            // with: a case-insensitive mark over a case-sensitive toggle draws a token as chosen
            // and then appends a duplicate when it is pressed.
            _filter.Categories.Contains(category.Value),
            () => ToggleAsync(_filter.Categories, category.Value,
                              values => _filter with { Categories = values }),
            []);
    }

    /// <summary>
    /// The catalogue's word for one EHDS token and its language, or the unmarked token where there is none.
    /// </summary>
    /// <remarks>
    /// A miss is shown rather than hidden, so the facet never offers fewer choices than the catalogue
    /// has. <see cref="CatalogueProperties.Option"/> says whether a label was curated.
    /// </remarks>
    private (string Text, string? Language) CategoryWord(string value) =>
        _vocabulary.TryGetValue(DataCategoryKey, out var entry)
        && CatalogueProperties.Option(entry, value, Reader) is { Curated: true } word
            ? (word.Label, Foreign(word.Language))
            : (value, null);

    /// <summary>The dataperiode facet — two date fields rather than a list of values.</summary>
    /// <remarks>
    /// Without a reported range it is drawn only while a date is set, so the control that applied a
    /// filter cannot vanish under it. (Fhi.Metadata-yxhv1)
    /// </remarks>
    private FacetGroup? DataPeriodGroup(FilterOptions facets)
    {
        // One per bound the reader has set, so a folded dataperiode says it is narrowing the way
        // every other facet does. Without it the summary reads plain "Dataperiode" over an active
        // date filter — the facet holds no values to count.
        var chosen = ChosenDates();
        var range = facets.DateRange;
        var reported = range is { } r && (r.Min is not null || r.Max is not null);

        // Drawn when the API reports a range, and drawn regardless whenever the reader has a date
        // set. A date filter matching nothing is exactly when the API stops reporting a range, so
        // dropping the facet then takes away the only control that can undo it. (Fhi.Metadata-yxhv1)
        if (!reported && chosen.Count == 0)
        {
            return null;
        }

        return new FacetGroup("dataperiode", T.FieldDataPeriod, OpenByDefault: false, [],
                              Body: DateFields(range ?? new DateInterval()), Chosen: chosen);
    }

    /// <summary>The bounds the reader has set, one value apiece.</summary>
    /// <remarks>
    /// Each names its own field rather than standing as a bare date: the two ends are drawn as one
    /// facet, so "1. jan. 2020" alone does not say which of them it is.
    /// </remarks>
    private IReadOnlyList<FacetValue> ChosenDates()
    {
        List<FacetValue> chosen = [];

        if (_filter.DataFrom is { } from)
        {
            chosen.Add(DateValue("date-from", T.FacetDateFrom, from,
                                 () => ApplyFilterAsync(_filter with { DataFrom = null })));
        }

        if (_filter.DataTo is { } to)
        {
            chosen.Add(DateValue("date-to", T.FacetDateTo, to,
                                 () => ApplyFilterAsync(_filter with { DataTo = null })));
        }

        return chosen;
    }

    /// <summary>One bound, written the way the reader's own language writes a day.</summary>
    /// <remarks>
    /// Unmarked, since the field's name and the date are both in the reader's language; narrow, so a
    /// chip reads like the results.
    /// </remarks>
    private FacetValue DateValue(string key, string field, DateOnly date, Func<Task> clear) =>
        new(key,
            T.FilterInFacet(field, CatalogueDate.Day(date, Language, DateWidth.Narrow)),
            Language: null,
            null,
            Selected: true,
            clear,
            []);

    /// <summary>The from and to fields, each bounded by the range and by the other.</summary>
    /// <remarks>
    /// Two text inputs rather than a range control, because Stiler has no date-range widget.
    /// Every class name here is borrowed and already used elsewhere in this component.
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

    /// <summary>One date field: label, format hint, text input, and a sentence when an entry is refused.</summary>
    /// <remarks>
    /// Text rather than <c>type="date"</c>, whose format follows the browser's locale, not the page's
    /// language. A refused entry is kept so the reader sees it beside the reason.
    /// </remarks>
    private void DateField(
        RenderTreeBuilder builder, int seq, string id, string label, DateOnly? value,
        DateOnly? min, DateOnly? max, Func<DateOnly?, Task> set)
    {
        var hintId = $"{id}-hint";
        var errorId = $"{id}-error";
        var refused = _refusedDates.TryGetValue(id, out var typedText);
        var shown = refused ? typedText! : DateInput.Format(value, Language);

        builder.OpenElement(seq, "label");
        builder.AddAttribute(seq + 1, "class", "form-element__label");
        builder.AddAttribute(seq + 2, "for", id);
        builder.AddContent(seq + 3, label);
        builder.CloseElement();

        builder.OpenElement(seq + 5, "p");
        builder.AddAttribute(seq + 6, "id", hintId);
        builder.AddAttribute(seq + 7, "class", "caption");
        builder.AddContent(seq + 8, T.FacetDateFormat);
        builder.CloseElement();

        builder.OpenElement(seq + 10, "input");
        builder.AddAttribute(seq + 11, "id", id);
        builder.AddAttribute(seq + 12, "type", "text");
        builder.AddAttribute(seq + 13, "autocomplete", "off");
        builder.AddAttribute(seq + 14, "placeholder", T.FacetDateFormat);
        builder.AddAttribute(seq + 15, "value", shown);
        builder.AddAttribute(seq + 16, "aria-invalid", refused ? "true" : null);
        builder.AddAttribute(seq + 17, "aria-describedby", refused ? $"{hintId} {errorId}" : hintId);

        // onchange, not oninput: a partly typed date is not a date. The awaiting binder overload, so a
        // failed fetch reaches the panel's alert region rather than surfacing as an unobserved task.
        builder.AddAttribute(seq + 18, "onchange",
            EventCallback.Factory.CreateBinder<string?>(this, raw =>
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    _refusedDates.Remove(id);

                    return set(null);
                }

                if (DateInput.TryParse(raw, Language, out var typed) && Within(typed, min, max))
                {
                    _refusedDates.Remove(id);

                    return set(typed);
                }

                _refusedDates[id] = raw;

                return Task.CompletedTask;
            }, shown));

        // The field rewrites what was typed — 5.3.2020 becomes 05.03.2020 — and when that is the
        // value already rendered, the diff alone would leave the reader's spelling in the box.
        builder.SetUpdatesAttributeName("value");
        builder.CloseElement();

        // Always rendered and empty until needed: change fires as focus leaves, so the reader is
        // elsewhere by then, and a role="alert" inserted and filled in one update is announced
        // unreliably. The infobox is inside, so an unrefused field draws no empty box.
        builder.OpenElement(seq + 20, "div");
        builder.AddAttribute(seq + 21, "id", errorId);
        builder.AddAttribute(seq + 22, "role", "alert");
        builder.AddAttribute(seq + 23, "aria-live", "assertive");
        builder.AddAttribute(seq + 24, "aria-atomic", "true");

        if (refused)
        {
            builder.OpenElement(seq + 25, "p");
            builder.AddAttribute(seq + 26, "class", "infobox infobox--bg-yellow");
            builder.AddContent(seq + 27, T.FacetDateInvalid(
                min is { } lo ? DateInput.Format(lo, Language) : null,
                max is { } hi ? DateInput.Format(hi, Language) : null));
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    /// <summary>Whether a typed date is inside the bounds the field itself advertises.</summary>
    /// <remarks>
    /// Any four digits make a year, so 0002 is a date the API would search and would empty the list
    /// with. (Fhi.Metadata-yxhv1)
    /// </remarks>
    private static bool Within(DateOnly? value, DateOnly? min, DateOnly? max) =>
        value is not { } date || ((min is not { } lo || date >= lo) && (max is not { } hi || date <= hi));

    private string DateFromId => $"munin-explorer-date-from-{_instance}";

    private string DateToId => $"munin-explorer-date-to-{_instance}";

    /// <summary>What the reader typed into a date field that was refused, by the field's id.</summary>
    private readonly Dictionary<string, string> _refusedDates = [];

    /// <summary>A reported bound as the date it names, without asking what time zone anyone is in.</summary>
    /// <remarks>
    /// <see cref="DateTimeOffset.Date"/> keeps <c>2020-01-01T00:00:00+02:00</c> on 1 January; UTC would
    /// make it 31 December. The same conversion <see cref="VariableFilter.DataFrom"/> prescribes.
    /// </remarks>
    private static DateOnly? Bound(DateTimeOffset? instant) =>
        instant is { } value ? DateOnly.FromDateTime(value.Date) : null;

    /// <summary>The kildetype facet — one value each, and only one of them can be chosen.</summary>
    /// <remarks>
    /// Closed at first paint like every facet but kilde: a panel that opens with everything
    /// expanded is what pushed the results off the screen. (Fhi.Metadata-l9l2n.67)
    /// </remarks>
    private FacetGroup KildeTypeGroup(FilterOptions facets) =>
        new("kildetype", T.FacetKildeType, OpenByDefault: false, [.. facets.KildeTyper.Select(KildeTypeValue)]);

    /// <summary>The kilde tree with each value told how many values beneath it are chosen.</summary>
    /// <remarks>An unchosen value with a choice beneath it draws half-ticked and says the count in words. (cjezd)</remarks>
    private static IReadOnlyList<FacetValue> WithChosenBelow(IReadOnlyList<FacetValue> values) =>
        [.. values.Select(value =>
        {
            var children = WithChosenBelow(value.Children);
            var below = children.Sum(child => (child is { Selected: true, Toggle: not null } ? 1 : 0) + child.ChosenBelow);
            return value with { Children = children, ChosenBelow = below };
        })];

    /// <summary>One kildetype, in the reader's own language whichever source names it.</summary>
    /// <remarks>
    /// Unmarked either way: the API answers in the language asked for, the fallback table is this
    /// package's own, and a bare enum token belongs to no language.
    /// </remarks>
    private FacetValue KildeTypeValue(KildetypeFacet type) =>
        new($"kildetype:{type.Value}",
            T.KildeTypeNameFromApi(type.Value, type.DisplayName),
            Language: null,
            Counted(type.Count),
            string.Equals(_filter.KildeType, type.Value, StringComparison.OrdinalIgnoreCase),
            () => SetKildeTypeAsync(type.Value),
            []);

    /// <summary>
    /// The kilde facet: kilder grouped under their kildetype, each with its own tree beneath it.
    /// </summary>
    /// <remarks>
    /// Built from the facet payload alone, whose counts the API cross-filters — unlike the hierarchy
    /// endpoint's kilde totals, which is why the level is drawn from facets at all.
    /// </remarks>
    private FacetGroup KildeGroup(FilterOptions facets)
    {
        var levels = FilterHierarchy.KildeLevels(facets);
        var tree = KildeTree(facets);
        var kilder = VisibleKilder(facets, tree);

        // The order the kildetype facet is in, so the headings here and the facet above agree.
        // That order is the API's own, and against runa on 2026-09-10 it followed the resolved
        // displayName — so it is the answering language's, not the enum's. (Fhi.Metadata-iv9xp)
        var kildeTypeOrder = facets.KildeTyper
            .Select((type, index) => (type.Value, Index: index))
            .ToDictionary(entry => entry.Value, entry => entry.Index, StringComparer.OrdinalIgnoreCase);

        var grouped = kilder
            .GroupBy(KildeTypeKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => kildeTypeOrder.TryGetValue(group.Key, out var index) ? index : int.MaxValue)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => KildeTypeHeading(facets, group, tree))
            .ToList();

        // A search that matches nothing has to leave the facet standing, or it would take the box
        // the reader must widen the term in away with it. Only while there is a term: the facet
        // still drops out when the API itself returned no kilder. (Fhi.Metadata-l9l2n.67)
        var empty = KildeSearchTerm is null ? null : T.FacetSearchNoMatch;

        // With one kildetype in the list its heading says nothing the facet above does not — and it
        // is exactly one whenever a kildetype has been chosen, which is when the panel is most
        // crowded. So the kilder are lifted out of it.
        var values = WithChosenBelow(grouped.Count == 1 ? grouped[0].Children : grouped);

        // The top level as it stands with no term, because that is the length a lifted cap is
        // recorded against: recording the survivors of a search would put the cap back over the
        // whole facet the moment the reader cleared the box.
        var unsearched = KildeSearchTerm is null ? values.Count : TopLevelKilder(ListedKilder(facets));

        return new FacetGroup(FacetName(HierarchyLevel.Kilde), T.FieldSource, OpenByDefault: true, values,
                              EmptyText: empty, Chosen: ChosenKilder(facets, levels),
                              Searchable: true, ValuesBeforeSearch: unsearched);
    }

    /// <summary>Which of the three source levels are ticked, whatever the facet's own search is showing.</summary>
    /// <remarks>
    /// Read off the answer, so a ticked kilde the search hides still counts (Fhi.Metadata-uidue); one
    /// entry per id, so no chip names a filter twice (Fhi.Metadata-l9l2n.82).
    /// </remarks>
    private IReadOnlyList<FacetValue> ChosenKilder(FilterOptions facets, KildeLevelLookup levels) =>
    [
        .. ListedKilder(facets)
            .Where(kilde => _filter.KildeIds.Contains(kilde.Id))
            .Select(kilde => KildeValue(kilde)),
        .. ListedKilder(facets)
            .SelectMany(kilde => levels.Delkilder[kilde.Id])
            .Where(delkilde => _filter.DelkildeIds.Contains(delkilde.Id))
            .Select(DelkildeValue),
        .. facets.Datasamlinger
            .DistinctBy(datasamling => datasamling.Id)
            .Where(datasamling => _filter.DatasamlingIds.Contains(datasamling.Id))
            .Select(DatasamlingValue)
    ];

    /// <summary>The payload's kilder, an id it names more than once standing for one kilde.</summary>
    /// <remarks>
    /// Two entries with one id are two keyed siblings, and the renderer throws. (Fhi.Metadata-l9l2n.82)
    /// </remarks>
    private static IReadOnlyList<KildeFacet> ListedKilder(FilterOptions facets) =>
        [.. facets.Kilder.DistinctBy(kilde => kilde.Id)];

    /// <summary>What the reader has typed into the kilde facet's own search box.</summary>
    private string _kildeSearch = string.Empty;

    /// <summary>The same, as it counts: null for a box holding nothing that could narrow anything.</summary>
    private string? KildeSearchTerm =>
        string.IsNullOrWhiteSpace(_kildeSearch) ? null : _kildeSearch.Trim();

    // The kilde tree and the filters answer it was built from, held as one so neither can name a
    // payload the other did not: see KildeTree for what it saves and FetchFacetsAsync for the clear
    // that releases it, which is what keeps identity from being the only way out.
    private FilterOptions? _treeOf;
    private IReadOnlyDictionary<Guid, HierarchyNode>? _kildeTree;

    /// <summary>The kilde facet's search box, named by a label of its own.</summary>
    private string KildeSearchId => $"munin-explorer-facet-search-{_instance}";

    /// <summary>The box itself, so focus can be put back on it before the values beside it are rewritten.</summary>
    private ElementReference _kildeSearchField;

    /// <summary>Record what was typed into the kilde facet's search box.</summary>
    /// <remarks>
    /// Focus first: a narrowing commit rewrites the list a reader who tabbed out is standing in
    /// (Fhi.Metadata-6we8a). <see cref="_filter"/> is untouched, so a hidden tick is never released.
    /// </remarks>
    private async Task SearchKilderAsync(string? text)
    {
        if (RemovesDrawnKilder(text))
        {
            await _kildeSearchField.FocusAsync();
        }

        _kildeSearch = text ?? string.Empty;

        OpenBranchesToMatches();
    }

    /// <summary>Whether committing <paramref name="text"/> takes a kilde the panel is drawing off the screen.</summary>
    /// <remarks>A widening commit, or one leaving every drawn kilde, removed nothing, so focus stays put.</remarks>
    private bool RemovesDrawnKilder(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _facets is not { } facets)
        {
            return false;
        }

        var term = text.Trim();
        var tree = KildeTree(facets);

        return VisibleKilder(facets, tree).Any(kilde => !KildeMatches(kilde, tree, term));
    }

    /// <summary>The kilder the facet's own search leaves, each with its whole tree under it.</summary>
    /// <remarks>
    /// A name anywhere below a kilde counts as the kilde's own, or typing a visible name would empty
    /// the facet. Ordinal ignore-case, as the kildeutforsker's facet search compares.
    /// </remarks>
    private IReadOnlyList<KildeFacet> VisibleKilder(
        FilterOptions facets, IReadOnlyDictionary<Guid, HierarchyNode> tree)
    {
        if (KildeSearchTerm is not { } term)
        {
            return ListedKilder(facets);
        }

        return [.. ListedKilder(facets).Where(kilde => KildeMatches(kilde, tree, term))];
    }

    /// <summary>Whether a kilde, or anything drawn under it, holds <paramref name="term"/>.</summary>
    /// <remarks>
    /// Variabelgrupper included, since each is on screen once its branch is open; revealing a deep
    /// match is <see cref="OpenBranchesToMatches"/>'s business.
    /// </remarks>
    private bool KildeMatches(KildeFacet kilde, IReadOnlyDictionary<Guid, HierarchyNode> tree, string term) =>
        LabelMatches(T.Named(kilde.Name, kilde.ShortName).Text, term)
        || (tree.TryGetValue(kilde.Id, out var node)
            && node.Children.Any(child => NodeMatches(child, term)));

    private bool NodeMatches(HierarchyNode node, string term) =>
        LabelMatches(T.Named(node.Name, node.ShortName).Text, term)
        || node.Children.Any(child => NodeMatches(child, term));

    private static bool LabelMatches(string label, string term) =>
        label.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>How many rows <paramref name="kilder"/> would put on the facet's top level.</summary>
    /// <remarks>
    /// <see cref="KildeGroup"/>'s grouping counted rather than built: one kildetype lifts its kilder
    /// out of the heading.
    /// </remarks>
    private static int TopLevelKilder(IReadOnlyList<KildeFacet> kilder)
    {
        var types = kilder.Select(KildeTypeKey).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        return types == 1 ? kilder.Count : types;
    }

    /// <summary>A kilde's kildetype, or the empty string when it has none — never null, so it can be a key.</summary>
    private static string KildeTypeKey(KildeFacet kilde) =>
        string.IsNullOrWhiteSpace(kilde.KildeType) ? "" : kilde.KildeType;

    /// <summary>A kildetype heading: a label rather than a filter, because kildetype has its own facet.</summary>
    /// <remarks>
    /// Shut at rest, and counting the kilder it hides rather than variables. (Fhi.Metadata-l9l2n.67)
    /// </remarks>
    private FacetValue KildeTypeHeading(
        FilterOptions facets,
        IGrouping<string, KildeFacet> kilder,
        IReadOnlyDictionary<Guid, HierarchyNode> tree) =>
        new($"kildetype-group:{kilder.Key}",
            KildeTypeNameFromApi(facets, kilder.Key),
            Language: null,
            kilder.Count(),
            Selected: false,
            Toggle: null,
            [.. kilder.Select(kilde => KildeValue(kilde, tree))],
            GroupHeading: true);

    private FacetValue KildeValue(KildeFacet kilde, IReadOnlyDictionary<Guid, HierarchyNode> tree) =>
        KildeValue(kilde) with
        {
            Count = Counted(kilde.Count),
            Children = tree.TryGetValue(kilde.Id, out var node) ? HierarchyValues(node.Children) : [],
        };

    /// <summary>A kilde on its own: its words and its toggle, with neither a count nor its tree.</summary>
    /// <remarks>
    /// The one reading of both, so a chip and the checkbox it stands for can never name one kilde
    /// two ways — its language included. The count is the drawn value's alone — a chip carries none.
    /// </remarks>
    private FacetValue KildeValue(KildeFacet kilde)
    {
        var (label, language) = CatalogueName(kilde.Name, kilde.ShortName);

        return new(FacetValueKey(HierarchyLevel.Kilde, kilde.Id),
            label,
            language,
            null,
            _filter.KildeIds.Contains(kilde.Id),
            () => ToggleAsync(_filter.KildeIds, kilde.Id, ids => _filter with { KildeIds = ids }),
            [],
            // The same folder a delkilde wears: the two are one level of grouping as far as the
            // tree is concerned, and Kelda's own hierarchy draws them alike.
            Icons: [DataCategoryIcons.Folder],
            Badge: T.KildeTypeBadge(kilde.KildeType));
    }

    /// <summary>What hangs under each kilde, by kilde id: the whole tree the builder places.</summary>
    /// <remarks>
    /// From one filters answer, so variabelgrupper cost no request of their own. (Fhi.Metadata-raspm)
    /// </remarks>
    private IReadOnlyDictionary<Guid, HierarchyNode> KildeTree(FilterOptions facets)
    {
        // Held against the answer by identity, which FilterOptions being an immutable record makes
        // sound: the walk over every placement the payload carries runs once per answer rather than
        // three times per commit of the facet's search box.
        if (ReferenceEquals(_treeOf, facets) && _kildeTree is { } held)
        {
            return held;
        }

        var tree = FilterHierarchy.ById(FilterHierarchy.Build(facets), node => node.Id);

        (_treeOf, _kildeTree) = (facets, tree);

        return tree;
    }

    /// <summary>The levels under a kilde as the panel draws them, each where the builder placed it.</summary>
    /// <remarks>
    /// <see cref="FacetList"/> gives a disclosure to whatever has children (Fhi.Metadata-adog5), and
    /// <see cref="FilterHierarchy.Build"/> places each node once, so the walk needs no guard.
    /// </remarks>
    private IReadOnlyList<FacetValue> HierarchyValues(IReadOnlyList<HierarchyNode> nodes)
    {
        // Read once rather than per node: HierarchyLevels is a fresh list of four records on every
        // read, and one kilde's tree runs to hundreds of nodes in the catalogue's larger registers.
        var readings = HierarchyLevels.ToDictionary(reading => reading.Level);

        return [.. nodes.Select(Value)];

        FacetValue Value(HierarchyNode node)
        {
            var (label, language) = CatalogueName(node.Name, node.ShortName);
            IReadOnlyList<FacetValue> children = [.. node.Children.Select(Value)];

            // A guard and not a row the tree draws: HierarchyLevels reads all four levels the
            // builder places. A fifth added to one list and not the other lands here as an inert
            // row rather than tearing the circuit down on a KeyNotFoundException.
            if (!readings.TryGetValue(node.Level, out var reading))
            {
                return new FacetValue(
                    NodeKey(node), label, language, Count: null, Selected: false, Toggle: null, children,
                    Icons: Glyphs(node));
            }

            // A variabelgruppe ticks under the placement it is drawn at, as Runa does: a bare id
            // would filter the group in every datasamling of the kilde. (Fhi.Metadata-59dh0)
            if (node is { Level: HierarchyLevel.Variabelgruppe, OwnerId: { } owner })
            {
                return new FacetValue(NodeKey(node), label, language, Counted(node.Count),
                                      IsPlacementChosen(node.Id, owner),
                                      () => TogglePlacementAsync(node.Id, owner),
                                      children,
                                      Icons: Glyphs(node));
            }

            return new FacetValue(NodeKey(node),
                                  label,
                                  language,
                                  Counted(node.Count),
                                  reading.Chosen().Contains(node.Id),
                                  () => ToggleAsync(reading.Chosen(), node.Id, reading.Apply),
                                  children,
                                  Icons: Glyphs(node));
        }

        // A delkilde wears the folder its kilde does: the two are one level of grouping as far as
        // the tree is concerned. Every other level asks for the categories the builder carries on
        // the datasamling alone, and draws none where there are none.
        static IReadOnlyList<NodeIcon> Glyphs(HierarchyNode node) =>
            node.Level == HierarchyLevel.Delkilde
                ? [DataCategoryIcons.Folder]
                : DataCategoryIcons.For(node.Categories);
    }

    /// <summary>What tells one drawn row of the kilde tree from every other.</summary>
    /// <remarks>
    /// A group drawn under several datasamlinger is keyed by placement, or they would share an expansion
    /// and an <c>id</c>. <see cref="FacetName"/>'s prefix keeps a path from reading as a value key.
    /// </remarks>
    private static string NodeKey(HierarchyNode node) =>
        node.Level == HierarchyLevel.Variabelgruppe
            ? $"{FacetName(node.Level)}:{node.Path}"
            : FacetValueKey(node.Level, node.Id);

    /// <summary>A delkilde as a chip names it: its words and its toggle, with no count, no tree and
    /// no folder. <see cref="ChosenKilder"/> is its one caller — the tree draws its delkilder, and
    /// puts their folders on, through <see cref="HierarchyValues"/>.</summary>
    private FacetValue DelkildeValue(DelkildeFacet delkilde)
    {
        var (label, language) = DelkildeLabel(delkilde);

        return new(FacetValueKey(HierarchyLevel.Delkilde, delkilde.Id),
            label,
            language,
            null,
            IsDelkildeChosen(delkilde.Id),
            ToggleDelkilde(delkilde.Id),
            []);
    }

    private (string Text, string? Language) DelkildeLabel(DelkildeFacet delkilde) =>
        CatalogueName(delkilde.Name, null);

    /// <summary>A datasamling the same way, and out of the same one caller.</summary>
    private FacetValue DatasamlingValue(DatasamlingFacet datasamling)
    {
        var (label, language) = DatasamlingLabel(datasamling);

        return new(FacetValueKey(HierarchyLevel.Datasamling, datasamling.Id),
            label,
            language,
            null,
            _filter.DatasamlingIds.Contains(datasamling.Id),
            () => ToggleAsync(_filter.DatasamlingIds, datasamling.Id,
                              ids => _filter with { DatasamlingIds = ids }),
            [],
            // Off the facet payload this row was already built from, so the glyphs cost no request —
            // and read whatever parent the row hangs from, since a category is the datasamling's own.
            Icons: DataCategoryIcons.For(datasamling.Categories));
    }

    private (string Text, string? Language) DatasamlingLabel(DatasamlingFacet datasamling) =>
        CatalogueName(datasamling.Name, null);

    private bool IsDelkildeChosen(Guid id) => _filter.DelkildeIds.Contains(id);

    private Func<Task> ToggleDelkilde(Guid id) =>
        () => ToggleAsync(_filter.DelkildeIds, id, ids => _filter with { DelkildeIds = ids });

    /// <summary>The variabelgruppe facet, as a tree.</summary>
    /// <remarks>
    /// Its empty state is a message: with no source chosen the API answers with a curated shortlist
    /// that may be empty, and "pick a datakilde" stops that reading as broken.
    /// </remarks>
    private FacetGroup VariabelgruppeGroup(FilterOptions facets)
    {
        // Collapsed before the tree is built, so the copy that decides whether a row is offered is
        // the copy that names it; Tree collapses the same way again, to no effect. (Fhi.Metadata-l9l2n.82)
        var grupper = FacetVariabelgrupper(facets);

        // An opted-out group is in this payload only to carry the offered groups under it, so it is
        // a container here: a checkbox would offer a filter the API says the reader may not have,
        // and dropping the row would strand its children.
        var containers = grupper
            .Where(gruppe => !gruppe.IsStandaloneFacetOption)
            .Select(gruppe => gruppe.Id)
            .ToHashSet();

        return new(FacetName(HierarchyLevel.Variabelgruppe),
            T.FieldVariableGroup,
            OpenByDefault: false,
            Tree(grupper.Select(g => Node(g.Id, g.ParentId, CatalogueName(g.Name, null), g.Count)),
                 $"{FacetName(HierarchyLevel.Variabelgruppe)}:",
                 IsGruppeChosen,
                 id => containers.Contains(id) ? null : ToggleGruppe(id), Counted),
            T.NoVariabelgrupper,
            Chosen: ChosenVariabelgrupper(facets));
    }

    /// <summary>Which variabelgrupper are ticked, whichever surface the reader ticked them on.</summary>
    /// <remarks>Read off the answer and not off this facet's own values, which withhold the groups
    /// the kilde tree offers; one entry per id, or one tick stands beside two chips.</remarks>
    private IReadOnlyList<FacetValue> ChosenVariabelgrupper(FilterOptions facets) =>
    [
        .. ListedVariabelgrupper(facets)
            .Where(gruppe => ChosenVariabelgruppeIds().Contains(gruppe.Id))
            .Select(VariabelgruppeValue)
    ];

    /// <summary>The standalone facet's own collection, one entry per id — the one call site of that
    /// collapse, so the row and the chip over one id read one list rather than agreeing by luck.</summary>
    private static IReadOnlyList<VariabelgruppeFacet> FacetVariabelgrupper(FilterOptions facets) =>
        FilterHierarchy.OnePerId(facets.Variabelgrupper, gruppe => gruppe.Id, gruppe => gruppe.ParentId);

    /// <summary>Every variabelgruppe either surface can name, one entry per id.</summary>
    /// <remarks>The standalone facet's copy wins, since copies of one id differ in name as well as
    /// in parent and <see cref="VariabelgruppeName"/> reads this list. (Fhi.Metadata-km3zb)</remarks>
    private static IReadOnlyList<VariabelgruppeFacet> ListedVariabelgrupper(FilterOptions facets)
    {
        var standalone = FacetVariabelgrupper(facets);

        var named = standalone.Select(gruppe => gruppe.Id).ToHashSet();

        return
        [
            .. standalone,
            .. FilterHierarchy
                .OnePerId(facets.HierarchyVariabelgrupper, gruppe => gruppe.Id, gruppe => gruppe.ParentId)
                .Where(gruppe => !named.Contains(gruppe.Id))
        ];
    }

    /// <summary>A variabelgruppe as a chip names it: its words and its toggle, with neither a count
    /// nor a tree. <see cref="ChosenVariabelgrupper"/> is its one caller — the facet draws its own
    /// values through <see cref="Tree"/>.</summary>
    private FacetValue VariabelgruppeValue(VariabelgruppeFacet gruppe)
    {
        var (label, language) = CatalogueName(gruppe.Name, null);

        // The chip stands for the group wherever it was ticked, so pressing it takes off every placement.
        return new(FacetValueKey(HierarchyLevel.Variabelgruppe, gruppe.Id),
            label,
            language,
            null,
            true,
            () => ApplyFilterAsync(KeepVariabelgrupper([.. ChosenVariabelgruppeIds().Where(id => id != gruppe.Id)])),
            []);
    }

    // Set when a scoped tick is refused at the cap, which would otherwise leave the box unticked without a word;
    // any filter change clears it, as Runa's hint does. (Fhi.Metadata-9s75x)
    private bool _scopeLimitRefused;

    private string ScopeLimitText => T.VariabelgruppeScopeLimit(VariableFilter.MaxVariabelgruppeScopes);

    private bool IsGruppeChosen(Guid id) => _filter.VariabelgruppeIds.Contains(id);

    private bool IsPlacementChosen(Guid id, Guid owner) =>
        IsGruppeChosen(id) || _filter.VariabelgruppeScopes.Contains(new VariabelgruppeScope(id, owner));

    /// <summary>Tick or untick a group under one placement.</summary>
    /// <remarks>A group chosen everywhere is one filter, so unticking any placement takes it off.
    /// A tick past the API's cap does nothing, since sending it would fail the whole search.</remarks>
    private Task TogglePlacementAsync(Guid id, Guid owner)
    {
        if (IsGruppeChosen(id))
        {
            return ToggleGruppe(id)();
        }

        var scope = new VariabelgruppeScope(id, owner);

        if (!_filter.VariabelgruppeScopes.Contains(scope)
            && _filter.VariabelgruppeScopes.Count >= VariableFilter.MaxVariabelgruppeScopes)
        {
            _scopeLimitRefused = true;
            return Task.CompletedTask;
        }

        return ToggleAsync(_filter.VariabelgruppeScopes, scope, scopes => _filter with { VariabelgruppeScopes = scopes });
    }

    // Choosing a group everywhere supersedes its placements, so their scopes go with the tick.
    private Func<Task> ToggleGruppe(Guid id) =>
        () => ToggleAsync(_filter.VariabelgruppeIds, id, ids => _filter with
        {
            VariabelgruppeIds = ids,
            VariabelgruppeScopes = [.. _filter.VariabelgruppeScopes.Where(scope => scope.VariabelgruppeId != id)]
        });

    /// <summary>The saved catalogue filters — see <see cref="FilterOptions.Filters"/> for why this is usually empty.</summary>
    private FacetGroup SavedFilterGroup(FilterOptions facets) =>
        new("filter",
            T.FacetFilter,
            OpenByDefault: false,
            Tree(facets.Filters
                     .Select(f => Node(f.Id, f.ParentId, CatalogueName(f.Name, null), f.Count)),
                 "filter:",
                 IsSavedFilterChosen,
                 ToggleSavedFilter, Counted));

    private bool IsSavedFilterChosen(Guid id) => _filter.FilterIds.Contains(id);

    private Func<Task> ToggleSavedFilter(Guid id) =>
        () => ToggleAsync(_filter.FilterIds, id, ids => _filter with { FilterIds = ids });

    private FacetGroup DataTypeGroup(FilterOptions facets) =>
        new("datatype", T.FacetDataType, OpenByDefault: false, [.. facets.DataTypes.Select(DataTypeValue)]);

    /// <summary>One datatype, in the reader's own language whichever source names it.</summary>
    /// <remarks>
    /// Unmarked because the API resolves the name in the language this package asked in, and the
    /// fallback for a nameless facet is the canonical code, which belongs to no language.
    /// </remarks>
    private FacetValue DataTypeValue(DataTypeFacet dataType) =>
        new($"datatype:{dataType.Value}",
            DataTypeFacetLabel(dataType),
            Language: null,
            Counted(dataType.Count),
            _filter.DataTypes.Contains(dataType.Value),
            () => ToggleAsync(_filter.DataTypes, dataType.Value, values => _filter with { DataTypes = values }),
            []);

    /// <summary>The word on a datatype facet button, on the same terms as the result rows.</summary>
    /// <remarks>
    /// AGENTS.md, "The API names a datatype, not this package". A nameless facet shows its canonical
    /// code, because a button labelled with a blank string has an empty accessible name.
    /// </remarks>
    private string DataTypeFacetLabel(DataTypeFacet dataType) =>
        string.IsNullOrWhiteSpace(dataType.DisplayName) ? T.CanonicalDataTypeCode(dataType.Value) : dataType.DisplayName;

    private FacetGroup HelsefagligKodeverkGroup(FilterOptions facets) =>
        new("helsefaglig-kodeverk",
            T.FacetHelsefagligKodeverk,
            OpenByDefault: false,
            [.. facets.HelsefagligKodeverk.Select(HelsefagligKodeverkValue)]);

    /// <summary>One helsefaglig kodeverk, by the short name the catalogue keys it on.</summary>
    /// <remarks>
    /// Unmarked, like a kilde's short name in <see cref="Texts.Named"/>: the keys mix Norwegian
    /// abbreviations and international tokens, and nothing here can tell which a key is.
    /// </remarks>
    private FacetValue HelsefagligKodeverkValue(HelsefagligKodeverkFacet kodeverk) =>
        new($"hk:{kodeverk.ShortName}",
            kodeverk.ShortName,
            Language: null,
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

    private FacetValue AdministrativtKodeverkValue(AdministrativtKodeverkFacet kodeverk)
    {
        // The OID when fhi.kodeverk could not be reached, because a nameless button is worse
        // than one labelled with the number the filter actually sends — and an OID is a number
        // rather than Norwegian, which is what CatalogueName marks the two apart by.
        var (label, language) = CatalogueName(kodeverk.Name, kodeverk.Oid);

        return new($"ak:{kodeverk.Oid}",
            label,
            language,
            Counted(kodeverk.Count),
            _filter.AdministrativtKodeverk.Contains(kodeverk.Oid),
            () => ToggleAsync(_filter.AdministrativtKodeverk, kodeverk.Oid,
                              values => _filter with { AdministrativtKodeverk = values }),
            []);
    }

    private FacetGroup InstrumentGroup(FilterOptions facets) =>
        new("instrument", T.FacetInstrument, OpenByDefault: false, [.. facets.Instruments.Select(InstrumentValue)]);

    private FacetValue InstrumentValue(InstrumentFacet instrument)
    {
        var (label, language) = CatalogueName(instrument.Name, instrument.Code);

        return new($"instrument:{instrument.Id}",
            label,
            language,
            Counted(instrument.Count),
            _filter.InstrumentIds.Contains(instrument.Id),
            () => ToggleAsync(_filter.InstrumentIds, instrument.Id, ids => _filter with { InstrumentIds = ids }),
            []);
    }

    /// <summary>The two filters that are a yes/no rather than a choice of values.</summary>
    /// <remarks>
    /// <c>NameInChips</c>, because "Har kildekodeverk" over the results without the facet in front
    /// of it reads as a property of the rows rather than as a filter on them. (Fhi.Metadata-l9l2n.68)
    /// </remarks>
    private FacetGroup OtherGroup(FilterOptions facets) =>
        new("other",
            T.FacetOther,
            OpenByDefault: false,
            [
                // Both unmarked: the words are this package's own, already in the reader's language.
                new FacetValue("has-kildekodeverk", T.HasKildekodeverk, Language: null,
                               Counted(facets.KildeKodeverkCount),
                               _filter.HasKildekodeverk == true, ToggleKildekodeverkAsync, []),

                // No count of its own: the API reports no facet for it, and the number it would
                // change is the total, which the status line already states.
                new FacetValue("include-historical", T.IncludeHistorical, Language: null, Count: null,
                               _filter.IncludeHistorical, ToggleHistoricalAsync, [])
            ],
            NameInChips: true);

    /// <summary>Turn a flat list of parented nodes into the tree the panel draws.</summary>
    /// <remarks>
    /// A missing parent makes its child a root, since cross-filtering drops parents; a cycle is seeded
    /// from what the first pass missed. A null <c>toggle</c> makes a container rather than a checkbox.
    /// </remarks>
    private static IReadOnlyList<FacetValue> Tree(
        IEnumerable<TreeNode> nodes,
        string keyPrefix,
        Func<Guid, bool> selected,
        Func<Guid, Func<Task>?> toggle,
        Func<int, int?> count)
    {
        var all = FilterHierarchy.OnePerId(nodes, node => node.Id, node => node.ParentId);

        if (all.Count == 0)
        {
            return [];
        }

        var known = all.Select(node => node.Id).ToHashSet();

        var byParent = all.Where(node => node.ParentId is not null).ToLookup(node => node.ParentId!.Value);
        HashSet<Guid> placed = [];

        List<FacetValue> roots = [];

        // Real roots first, then whatever they could not reach: every member of a cycle has its
        // parent present, so none of them is a root, and dropping them would take a filter off the
        // panel with no error anywhere.
        AddRoots(node => !Parented(node));
        AddRoots(_ => true);

        return roots;

        bool Parented(TreeNode node) => node.ParentId is { } parent && known.Contains(parent);

        void AddRoots(Func<TreeNode, bool> isRoot)
        {
            // A foreach rather than a query, because `placed` is a set the body mutates: building a
            // node places its whole subtree, so a cycle's other member is already drawn by the time
            // the second pass reaches it and must not be built as a root of its own as well.
            foreach (var node in all)
            {
                if (isRoot(node) && !placed.Contains(node.Id))
                {
                    roots.Add(Build(node));
                }
            }
        }

        FacetValue Build(TreeNode node)
        {
            placed.Add(node.Id);

            List<FacetValue> children = [];

            // Same shape as AddRoots above, and for the same reason: each child is tested against a
            // set the recursion mutates, so building one sibling can place the next.
            foreach (var child in byParent[node.Id])
            {
                if (!placed.Contains(child.Id))
                {
                    children.Add(Build(child));
                }
            }

            return new FacetValue($"{keyPrefix}{node.Id}", node.Label, node.Language, count(node.Count),
                                  selected(node.Id), toggle(node.Id), children);
        }
    }

    /// <summary>Which way the last Utvid alle / Skjul alle press left every disclosure, if any.</summary>
    private bool? _foldAll;

    /// <summary>Bumped per press, and part of every disclosure's key, so the press rebuilds them.</summary>
    /// <remarks>
    /// Hand-folded <c>&lt;details&gt;</c> no longer match the <c>open</c> we rendered, which is never re-patched.
    /// No test covers this: deleting it stays green and breaks a second press. (Fhi.Metadata-wcbxi)
    /// </remarks>
    private int _foldGeneration;

    /// <summary>A disclosure's key: its facet, and the fold press it was last rebuilt for.</summary>
    /// <remarks>
    /// The facet half stops a dropped facet handing its open state to its successor; between presses
    /// a filter change leaves open whatever the reader opened.
    /// </remarks>
    private string FacetKey(FacetGroup group) => $"{group.Key}#{_foldGeneration}";

    /// <summary>Whether a facet is drawn open: the last fold press, or the facet's own default.</summary>
    private bool FacetOpen(FacetGroup group) => _foldAll ?? group.OpenByDefault;

    /// <summary>The icon legend's disclosure key, on the generation the facets' keys carry.</summary>
    private string LegendKey => $"icon-legend#{_foldGeneration}";

    /// <summary>Whether the icon legend is drawn open. It folds with the facets rather than apart
    /// from them: one disclosure left standing open under a pressed Skjul alle reads as the press
    /// not having worked.</summary>
    private bool LegendOpen => _foldAll ?? false;

    /// <summary>What the last fold press did, for the panel's live region.</summary>
    /// <remarks>
    /// Empty until a press, or the region would speak on every mount. A second identical press is
    /// silent, which is the region working rather than failing: it already said that.
    /// </remarks>
    private string FoldAnnouncement => _foldAll switch
    {
        true => T.FacetsExpanded,
        false => T.FacetsCollapsed,
        _ => string.Empty
    };

    /// <summary>Whether the panel is unfolded on a narrow screen; a host with room for a sidebar shows it regardless.</summary>
    private bool _filtersOpen;

    private string FacetsId => $"munin-explorer-filters-{_instance}";

    // A double-click's second click would fold straight back what the first unfolded, as it did on
    // Kelda's toggle (Fhi.Metadata-zel47).
    private void ToggleFiltersFromControl(MouseEventArgs released)
    {
        if (!RowPress.WasSelectionStandingStill(released))
        {
            _filtersOpen = !_filtersOpen;
        }
    }

    /// <summary>Open every facet and every branch of every facet tree at once, or fold them all.</summary>
    /// <remarks>
    /// The rebuild loses a half-typed date, which lives only in the DOM; keying that facet apart would
    /// make the press skip it, which is worse.
    /// </remarks>
    private void FoldAll(bool open)
    {
        _foldAll = open;
        _foldGeneration++;

        FoldAllBranches(open);
        FoldAllFacetValues(open);
    }

    /// <summary>Whether the tree draws a guide line per level.</summary>
    /// <remarks>
    /// No initialiser on purpose: <see cref="LevelLines"/> is copied in here before the first
    /// render, so the parameter's default is the only place the resting state is written down.
    /// </remarks>
    private bool _levelLines;

    /// <summary>The panel's marker for the level lines, or null (no attribute) while they are off.</summary>
    /// <remarks>
    /// A data attribute because a class name is inventory to carry (Fhi.Metadata-wcbxi); a screen reader
    /// hears the switch's own <c>aria-checked</c> instead. (Fhi.Metadata-l9l2n.87)
    /// </remarks>
    private string? LevelLinesMarker => _levelLines ? "true" : null;

    /// <summary>Turn the level lines on or off, and tell the host, so it can remember them.</summary>
    private Task ToggleLevelLinesAsync()
    {
        _levelLines = !_levelLines;

        return RaiseAsync(LevelLinesChanged, _levelLines, Log);
    }

    /// <summary>Whether the kilde tree draws a node icon in front of each name.</summary>
    /// <remarks>
    /// No initialiser, for the reason <see cref="_levelLines"/> has none: <see cref="ShowNodeIcons"/>
    /// is copied in before the first render, so the parameter's default is the one resting state.
    /// </remarks>
    private bool _showNodeIcons;

    /// <summary>Turn the node icons on or off, and tell the host, so it can remember them.</summary>
    private Task ToggleNodeIconsAsync()
    {
        _showNodeIcons = !_showNodeIcons;

        return RaiseAsync(ShowNodeIconsChanged, _showNodeIcons, Log);
    }

    /// <summary>The words for the glyphs the tree draws: one row per datakategori.</summary>
    /// <remarks>
    /// The whole vocabulary, never the rows on screen, or the legend would read as a second facet.
    /// (Fhi.Metadata-zllxt)
    /// </remarks>
    private RenderFragment IconLegendList => builder =>
    {
        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "class", "munin-explorer-filters__legend");

        foreach (var icon in DataCategoryIcons.All)
        {
            builder.OpenElement(2, "li");
            builder.SetKey(icon.Key);
            builder.AddAttribute(3, "class", "munin-explorer-filters__legend-item");

            // A direct child of the row, which is what Stiler's rule selects: the slot a value row
            // wears would put a second box between this glyph and the word explaining it.
            builder.AddContent(4, (RenderFragment)(nested =>
                NodeIcons.WriteGlyph(nested, icon, NodeIconClasses.Facets.Glyph)));

            // The name in a box of its own, so a long one wraps beside the glyph rather than under
            // it — an anonymous flex item cannot be the thing a rule gives room to shrink.
            builder.OpenElement(5, "span");
            builder.AddContent(
                6, T.DataCategoryNames.TryGetValue(icon.Key, out var name) ? name : icon.Key);
            builder.CloseElement();

            builder.CloseElement();
        }

        builder.CloseElement();
    };

    /// <summary>A facet's own label, saying how many of its values are chosen.</summary>
    /// <remarks>So a collapsed facet still says something inside it is narrowing the list.</remarks>
    private static string GroupLabel(FacetGroup group) =>
        group.SelectedCount == 0 ? group.Label : $"{group.Label} ({group.SelectedCount})";

    /// <summary>Which facets the reader has asked to see the whole of, and how long each was then.</summary>
    /// <remarks>
    /// Keyed on the facet, since folding is not re-hiding; the length because keys outlive the values.
    /// See <see cref="FacetLimits.StillExpanded"/>.
    /// </remarks>
    private readonly Dictionary<string, int> _expandedFacets = new(StringComparer.Ordinal);

    /// <summary>Lift every facet's cap at once, or put all of them back.</summary>
    /// <remarks>
    /// Utvid alle reaches the branches already; it has to reach past the cap too, or the control
    /// that offers to open everything stops ten values into the kilde tree and says nothing of it.
    /// </remarks>
    private void FoldAllFacetValues(bool open)
    {
        _expandedFacets.Clear();

        if (!open)
        {
            return;
        }

        foreach (var group in FacetGroups)
        {
            // Only the facets the cap can apply to, asked through the same predicate every other
            // call site here asks, so the stored set holds no key a lifted cap could never honour.
            if (FacetLimits.IsLong(group.TotalValues))
            {
                _expandedFacets[group.Key] = group.TotalValues;
            }
        }
    }

    /// <summary>Whether the reader has pressed this facet's own "Vis N til", over these values.</summary>
    private bool IsFacetExpanded(FacetGroup group) =>
        FacetLimits.StillExpanded(
            _expandedFacets.TryGetValue(group.Key, out var asked) ? asked : null,
            group.TotalValues);

    /// <summary>Show the rest of a facet's values, or take them back behind the cap.</summary>
    /// <remarks>
    /// The standing-gesture clause every other disclosure here carries: the second click of a
    /// double-click and a shift-click both stand still, and neither is a press. (Fhi.Metadata-zel47)
    /// </remarks>
    private void ToggleFacetExpandedFromControl(FacetGroup group, MouseEventArgs released)
    {
        if (RowPress.WasSelectionStandingStill(released))
        {
            return;
        }

        if (IsFacetExpanded(group))
        {
            _expandedFacets.Remove(group.Key);
        }
        else
        {
            _expandedFacets[group.Key] = group.TotalValues;
        }
    }

    /// <summary>Whether a facet's own search box is narrowing it right now.</summary>
    /// <remarks>
    /// The kilde facet's values arrive already narrowed, and capping them would hide a value the reader
    /// typed the name of.
    /// </remarks>
    private bool IsFacetSearched(FacetGroup group) => group.Searchable && KildeSearchTerm is not null;

    /// <summary>The top-level values a facet draws, and how many its cap is holding back.</summary>
    /// <remarks>
    /// <see cref="FacetLimits"/>'s predicate, so "long" means the same as in Kelda's. Top level only:
    /// a branch is shut at rest already.
    /// </remarks>
    private CappedValues<FacetValue> VisibleValues(FacetGroup group) =>
        IsFacetSearched(group)
            ? FacetLimits.Uncapped(group.Values)
            : FacetLimits.Cap(group.Values, IsFacetExpanded(group), AnySelected);

    /// <summary>Whether a value, or anything nested under it, is ticked.</summary>
    /// <remarks>
    /// The whole subtree, because a ticked datasamling is only reachable through the kilde above
    /// it: a cap that kept the ticked row and dropped its ancestor would hide the filter anyway.
    /// </remarks>
    private static bool AnySelected(FacetValue value) =>
        value.Selected || value.Children.Any(AnySelected);

    /// <summary>Whether the facet draws the control that reveals what the cap is holding back.</summary>
    /// <remarks>
    /// Not while a search draws every match; an expanded facet keeps it as the only way back.
    /// <paramref name="hidden"/> is the render's own count, never a second sum.
    /// </remarks>
    private bool ShowsRestControl(FacetGroup group, int hidden) =>
        !IsFacetSearched(group)
        && FacetLimits.IsLong(group.Values.Count)
        && (IsFacetExpanded(group) || hidden > 0);

    /// <summary>What the control says: the remainder it would reveal, or the offer to put it back.</summary>
    private string RestControlText(FacetGroup group, int hidden) =>
        IsFacetExpanded(group) ? T.ShowFewerFacetValues : T.ShowMoreFacetValues(hidden);

    /// <summary>The id joining a facet's value list to the control that reveals the rest of it.</summary>
    private string FacetOptionsId(string key) => $"munin-explorer-facet-options-{_instance}-{key}";

    /// <summary>A facet's values as a nested list of checkboxes, keyed so a reorder never moves a box.</summary>
    /// <remarks>
    /// <c>lang</c> sits on the label, never the <c>&lt;li&gt;</c>, which would pass it to children. A shut
    /// branch's children are absent, not hidden, so none is tabbable. (Fhi.Metadata-j0a2h, Fhi.Metadata-adog5)
    /// </remarks>
    private RenderFragment FacetList(IReadOnlyList<FacetValue> values, string? id = null) => builder =>
    {
        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "id", id);

        foreach (var value in values)
        {
            var branch = value.Children.Count > 0;
            var open = branch && IsBranchOpen(value.Key);

            builder.OpenElement(2, "li");
            builder.SetKey(value.Key);

            if (branch)
            {
                builder.AddAttribute(3, "class", "munin-explorer-filters__branch");
                Disclosure(builder, value, open);
            }

            // Held in a local so the null check below is one the compiler can carry into the branch.
            var toggle = value.Toggle;

            if (toggle is null)
            {
                // A container names a variabelgruppe in the catalogue's own Norwegian and the chip
                // for that group is marked, so this row has to be too. A span with no class is
                // somewhere to hang the marking that costs no rule in Stiler.
                builder.OpenElement(20, "span");
                builder.AddAttribute(21, "lang", value.Language);
                builder.AddContent(22, value.Label);
                builder.CloseElement();

                // Outside the span above rather than inside it, which is where the summary this
                // replaced held it: the marking is the catalogue's language, and this figure is
                // ours. A group's SIZE, so `__groupcount` and never `__chosen`. (Fhi.Metadata-l9l2n.104)
                if (value is { GroupHeading: true, Count: { } members })
                {
                    builder.AddContent(23, " ");
                    builder.OpenElement(24, "span");
                    builder.AddAttribute(25, "class", "munin-explorer-filters__groupcount");
                    builder.AddContent(26, members.ToString(CultureInfo.CurrentCulture));
                    builder.CloseElement();
                }
            }
            else
            {
                builder.OpenElement(30, "label");
                builder.OpenElement(31, "input");
                builder.AddAttribute(32, "type", "checkbox");
                builder.AddAttribute(33, "checked", value.Selected);

                // The half-tick itself is a DOM property with no attribute, so markup carries this and
                // explorer-interop.js's markMixed sets it after render. (Fhi.Metadata-cjezd)
                var mixed = !value.Selected && value.ChosenBelow > 0;
                if (mixed)
                {
                    builder.AddAttribute(34, "data-mixed", "true");
                }

                // The event's own value is ignored: the toggle flips what the filter holds, which
                // is the one state a press and the render after it are certain to agree about.
                builder.AddAttribute(35, "onchange",
                                     EventCallback.Factory.Create<ChangeEventArgs>(this, _ => toggle()));

                // What a plain onchange does not do and this panel needs: a press that ApplyFilterAsync
                // drops mid-fetch writes no state, so the renders either side are equal and
                // the browser's own tick stays on over a filter that is off until `checked` is forced.
                builder.SetUpdatesAttributeName("checked");

                builder.CloseElement();

                // Both or neither, as KildeHierarchyView does it: the words are the glyphs said
                // aloud, so keeping them would leave the switch doing nothing for the reader who
                // pressed it. The badge below is a fact rather than a picture and is outside both.
                var icons = _showNodeIcons ? value.Icons : null;
                if (icons is { Count: > 0 })
                {
                    builder.AddContent(36, (RenderFragment)(nested =>
                        NodeIcons.Write(nested, icons, NodeIconClasses.Facets)));
                }

                // The marking sits on the name, not on the label around it: the label also carries
                // this package's own prose below, which is the reader's language and not the
                // catalogue's and must not be pronounced as Norwegian. (WCAG 3.1.2)
                builder.OpenElement(37, "span");
                builder.AddAttribute(38, "lang", value.Language);
                builder.AddContent(39, value.Label);
                builder.CloseElement();

                // The half-tick in words, inside the label so it is part of the checkbox's name.
                if (mixed)
                {
                    builder.AddContent(40, " ");
                    builder.OpenElement(41, "span");
                    builder.AddAttribute(42, "class", "screenreader-only");
                    builder.AddContent(43, T.ChosenBelow(value.ChosenBelow));
                    builder.CloseElement();
                }

                // Keep the spoken categories after the name even though the decorative icons lead it.
                if (icons is { Count: > 0 })
                {
                    builder.AddContent(44, (RenderFragment)(nested =>
                        NodeIcons.WriteSpoken(nested, icons, T)));
                }

                // Real text rather than a rule on the row, so the badge is part of the checkbox's
                // accessible name: what it says is a fact about the kilde and not decoration, and a
                // mark only a stylesheet drew would reach a sighted reader and nobody else.
                if (value.Badge is { } badge)
                {
                    builder.AddContent(45, " ");
                    builder.OpenElement(46, "span");
                    builder.AddAttribute(47, "class", "munin-explorer-filters__badge");
                    builder.AddContent(48, badge);
                    builder.CloseElement();
                }

                // The space is a text node of the label, not the span's first character, so a rule
                // dressing the count dresses the number and not the gap. The name keeps the space
                // either way (Fhi.Metadata-47lha).
                if (value.Count is { } count)
                {
                    builder.AddContent(49, " ");
                    builder.OpenElement(50, "span");
                    builder.AddAttribute(51, "class", "munin-explorer-filters__count");
                    builder.AddContent(52, $"({count})");
                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            if (open)
            {
                builder.AddContent(60, FacetList(value.Children, BranchId(value.Key)));
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    };

    /// <summary>The control that opens and shuts one branch of a facet tree.</summary>
    /// <remarks>
    /// Beside the checkbox, never around it, since expanding must not filter. <c>aria-controls</c> only
    /// while the list is drawn: a shut branch renders no children for it to name.
    /// </remarks>
    private void Disclosure(RenderTreeBuilder builder, FacetValue value, bool open)
    {
        builder.OpenElement(4, "button");
        builder.AddAttribute(5, "type", "button");
        builder.AddAttribute(6, "class", "munin-explorer-filters__disclosure");
        builder.AddAttribute(7, "aria-expanded", open ? "true" : "false");
        builder.AddAttribute(8, "aria-controls", open ? BranchId(value.Key) : null);
        var nameId = BranchId(value.Key) + "-name";
        builder.AddAttribute(9, "aria-labelledby", $"{nameId}-action {nameId}");

        var key = value.Key;

        builder.AddAttribute(10, "onclick",
                             EventCallback.Factory.Create<MouseEventArgs>(this, e => ToggleBranchFromControl(key, e)));

        // Text rather than a rule, so a host with no stylesheet still sees which way the branch
        // points — and aria-hidden, so the name is the label alone: an arrow is not the visible
        // word WCAG 2.5.3 asks a name to contain.
        builder.OpenElement(11, "span");
        builder.AddAttribute(12, "aria-hidden", "true");
        builder.AddContent(13, open ? "▾" : "▸");
        builder.CloseElement();

        // Keep catalogue words in their own language without applying it to the UI action.
        builder.OpenElement(14, "span");
        builder.AddAttribute(15, "class", "screenreader-only");
        builder.OpenElement(16, "span");
        builder.AddAttribute(17, "id", nameId + "-action");
        builder.AddAttribute(18, "lang", ReaderLanguage.Of(Language));
        builder.AddContent(19, open ? T.CollapseBranch : T.ExpandBranch);
        builder.CloseElement();
        builder.AddContent(20, " ");
        builder.OpenElement(21, "span");
        builder.AddAttribute(22, "id", nameId);
        builder.AddAttribute(23, "lang", value.Language ?? ReaderLanguage.Of(Language));
        builder.AddContent(24, value.Label);
        builder.CloseElement();
        builder.CloseElement();

        builder.CloseElement();
    }

    /// <summary>Which branches of the facet trees are open, by the value key of each.</summary>
    /// <remarks>
    /// Never cleared when a filters answer arrives, so a branch the reader opened is still open
    /// after ticking something inside it — the keys are the payload's own ids rather than positions.
    /// </remarks>
    private readonly HashSet<string> _expandedBranches = [];

    /// <summary>Whether the branch at <paramref name="key"/> is drawn open. Every branch starts shut.</summary>
    private bool IsBranchOpen(string key) => _expandedBranches.Contains(key);

    /// <summary>The id of the list one branch discloses, unique to this mount and to that branch.</summary>
    /// <remarks>Every segment is valid in a CSS selector, so a test or a host can write <c>#id</c>.</remarks>
    private string BranchId(string key) => $"munin-explorer-branch-{_instance}-{IdPart(key)}";

    /// <summary>One facet value key as an id can spell it, and no two keys alike.</summary>
    /// <remarks>
    /// A space in an API kildetype would split <c>aria-controls</c> into two ids. Escaped, not replaced,
    /// so keys differing only in punctuation stay apart.
    /// </remarks>
    private static string IdPart(string key)
    {
        var id = new StringBuilder(key.Length);

        foreach (var character in key)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '-')
            {
                id.Append(character);
            }
            else
            {
                id.Append(CultureInfo.InvariantCulture, $"_{(int)character:x4}");
            }
        }

        return id.ToString();
    }

    /// <summary>Open or shut one branch, and nothing else.</summary>
    /// <remarks>
    /// Leaves <see cref="_filter"/> alone, so ticks inside survive a reopen. Double- and shift-clicks are
    /// ignored; a click with no count is the keyboard's and goes through. (Fhi.Metadata-zel47)
    /// </remarks>
    private void ToggleBranchFromControl(string key, MouseEventArgs released)
    {
        if (RowPress.WasSelectionStandingStill(released))
        {
            return;
        }

        if (!_expandedBranches.Add(key))
        {
            _expandedBranches.Remove(key);
        }
    }

    /// <summary>Open every branch of every facet tree, or shut all of them.</summary>
    /// <remarks>
    /// Walked off <see cref="FacetGroups"/> rather than remembered as a flag, so a branch the
    /// reader shuts after Utvid alle stays shut the way a facet does.
    /// </remarks>
    private void FoldAllBranches(bool open)
    {
        _expandedBranches.Clear();

        if (!open)
        {
            return;
        }

        foreach (var group in FacetGroups)
        {
            OpenBranches(group.Values);
        }
    }

    /// <summary>Open every branch under <paramref name="values"/>, and everything under those.</summary>
    /// <remarks>
    /// Descends only into a key it had not already opened, so a payload whose nodes name each other
    /// as parent terminates here: a stack overflow is process-fatal and cannot be caught.
    /// </remarks>
    private void OpenBranches(IReadOnlyList<FacetValue> values)
    {
        foreach (var value in values)
        {
            if (value.Children.Count > 0 && _expandedBranches.Add(value.Key))
            {
                OpenBranches(value.Children);
            }
        }
    }

    /// <summary>Open the branches standing between a kilde the search kept and the name that matched.</summary>
    /// <remarks>
    /// Branches start shut, so a deep match would otherwise keep a row whose matching name is nowhere
    /// on screen. Written where a press writes, so Skjul alle still shuts it. (Fhi.Metadata-adog5)
    /// </remarks>
    private void OpenBranchesToMatches()
    {
        if (KildeSearchTerm is not { } term || _facets is not { } facets)
        {
            return;
        }

        OpenMatchedBranches(KildeGroup(facets).Values, term, []);
    }

    /// <summary>
    /// Whether anything at or under <paramref name="values"/> holds <paramref name="term"/>, opening
    /// every branch that has a match below it on the way back up.
    /// </summary>
    /// <remarks>A branch whose own label matched stays shut: its name is already on screen.</remarks>
    private bool OpenMatchedBranches(IReadOnlyList<FacetValue> values, string term, HashSet<string> walked)
    {
        var matched = false;

        foreach (var value in values)
        {
            var below = walked.Add(value.Key) && OpenMatchedBranches(value.Children, term, walked);

            if (below)
            {
                _expandedBranches.Add(value.Key);
            }

            matched = matched || below || LabelMatches(value.Label, term);
        }

        return matched;
    }

    /// <summary>Add or remove one value from a facet, and fetch what that leaves.</summary>
    /// <remarks><c>TItem</c>, not <c>T</c>, which would shadow the component's translations accessor.</remarks>
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

    /// <summary>Choose a kildetype, or clear it by choosing the one already chosen.</summary>
    /// <remarks>
    /// One at a time because the API takes one; a checkbox rather than a radio, since there is no
    /// "any kildetype" value to go back to.
    /// </remarks>
    private Task SetKildeTypeAsync(string value)
    {
        var chosen = string.Equals(_filter.KildeType, value, StringComparison.OrdinalIgnoreCase);

        return ApplyFilterAsync(_filter with { KildeType = chosen ? null : value });
    }

    /// <summary>Keep only variables that have a kildekodeverk link, or stop filtering on it.</summary>
    /// <remarks>
    /// Two states, not three: the API's <c>false</c> is a question nobody asks of a catalogue browser.
    /// </remarks>
    private Task ToggleKildekodeverkAsync() =>
        ApplyFilterAsync(_filter with { HasKildekodeverk = _filter.HasKildekodeverk == true ? null : true });

    private Task ToggleHistoricalAsync() =>
        ApplyFilterAsync(_filter with { IncludeHistorical = !_filter.IncludeHistorical });

    /// <summary>The chosen values as the row over the results draws them, in the panel's own order.</summary>
    /// <remarks>
    /// Every facet is walked, so one added later draws chips unasked. An unfaceted hierarchy value is
    /// spliced in where its facet stands, not appended — see <see cref="UnfacetedHierarchyChips"/>.
    /// </remarks>
    private IReadOnlyList<ActiveFilters.Chip> ActiveFilterChips
    {
        get
        {
            List<ActiveFilters.Chip> chips = [];
            HashSet<string> removable = [];
            HashSet<string> walked = [];

            foreach (var group in FacetGroups)
            {
                foreach (var value in group.ChosenValues)
                {
                    // A value with no checkbox has no removal to offer here: a kildetype heading is
                    // a label rather than a filter. Every chosen variabelgruppe carries one now, the
                    // opt-out included, so the fallback below is for one neither collection names.
                    if (value.Toggle is not { } toggle)
                    {
                        continue;
                    }

                    var (text, language) = ChipReading(group, value);

                    removable.Add(value.Key);
                    chips.Add(new ActiveFilters.Chip(
                        text, T.RemoveFilter(text), null, language, () => RemoveFilterAsync(toggle)));
                }

                walked.Add(group.Key);
                chips.AddRange(
                    UnfacetedHierarchyChips(removable, level => FacetGroupOf(level) == group.Key));
            }

            // The levels whose facet is not on screen at all, which is every level before the first
            // answer and after one that failed — the state a host mounting with Filter set lands in.
            chips.AddRange(
                UnfacetedHierarchyChips(removable, level => !walked.Contains(FacetGroupOf(level))));

            return chips;
        }
    }

    /// <summary>Chips for chosen values no facet drew, at levels <paramref name="drawnHere"/> admits.</summary>
    /// <remarks>
    /// Cross-filtering can leave a chosen value out of the payload, and without a chip only "Fjern alle
    /// filtre" removes it (Fhi.Metadata-oj286). Values a level cannot name share one counted chip.
    /// </remarks>
    private IReadOnlyList<ActiveFilters.Chip> UnfacetedHierarchyChips(
        IReadOnlySet<string> removable, Func<HierarchyLevel, bool> drawnHere)
    {
        List<ActiveFilters.Chip> chips = [];

        foreach (var level in HierarchyLevels.Where(level => drawnHere(level.Level)))
        {
            var missing = level.Chosen()
                .Where(id => !removable.Contains(FacetValueKey(level.Level, id)))
                .Select(id => (Id: id, Name: level.Name(id)))
                .ToList();

            // Every HierarchyReading.Name reads a facet's or a row's Name and never a code or a
            // short name, so a non-null one is the catalogue's Norwegian — the split the trail
            // records as HierarchyCrumb.Norwegian, the fallback below being this package's prose.
            foreach (var (id, name) in missing.Where(value => value.Name is not null))
            {
                chips.Add(Chip(name!, Foreign(ReaderLanguage.Norwegian),
                               () => ToggleAsync(level.Chosen(), id, level.Apply)));
            }

            // The level's own word where nothing on screen knows the value. Never the id: a guid
            // is not a name, and the chip still has to say which filter it takes off.
            var unnamed = missing.Where(value => value.Name is null).Select(value => value.Id).ToList();

            if (unnamed.Count == 0)
            {
                continue;
            }

            var text = unnamed.Count > 1
                ? T.CrumbMore(level.Fallback, unnamed.Count - 1)
                : level.Fallback;

            chips.Add(Chip(text, null,
                           () => ApplyFilterAsync(level.Apply([.. level.Chosen().Except(unnamed)]))));
        }

        return chips;

        ActiveFilters.Chip Chip(string text, string? language, Func<Task> remove) =>
            new(text, T.RemoveFilter(text), null, language, () => RemoveFilterAsync(remove));
    }

    /// <summary>How one chosen value reads in the chip row, and what language those words are in.</summary>
    /// <remarks>
    /// Both from <c>NameInChips</c>: a chip composed around the facet's heading is this package's prose
    /// in the reader's language, so a facet cannot get one half without the other.
    /// </remarks>
    private (string Text, string? Language) ChipReading(FacetGroup group, FacetValue value) =>
        group.NameInChips
            ? (T.FilterInFacet(group.Label, value.Label), null)
            : (value.Label, value.Language);

    /// <summary>Untick one value from the chip row, through the state its own checkbox writes.</summary>
    /// <remarks>
    /// Focus first, to the search field: the chip leaves as it acts, and focus would fall to
    /// <c>&lt;body&gt;</c>. (Fhi.Metadata-ag4n7)
    /// </remarks>
    private async Task RemoveFilterAsync(Func<Task> toggle)
    {
        await _searchField.FocusAsync();

        await toggle();
    }

    /// <summary>Drop every filter and fetch the whole search again.</summary>
    /// <remarks>
    /// Focus moves first, for the reason <see cref="RemoveFilterAsync"/> gives. (Fhi.Metadata-l9l2n.68)
    /// </remarks>
    private async Task ClearFiltersAsync()
    {
        await _searchField.FocusAsync();

        await ApplyFilterAsync(VariableFilter.None);
    }

    /// <summary>Apply <paramref name="next"/>: fetch what it leaves, and refresh the counts beside it.</summary>
    /// <remarks>
    /// The one way the filter changes, so page reset, rollback and host notification are written once.
    /// </remarks>
    private async Task ApplyFilterAsync(VariableFilter next)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit, a
        // sort click and a page turn.
        if (_loading)
        {
            return;
        }

        // A press that asks for the filter already in force costs no request — clearing an empty
        // selection, say. VariableFilter compares by what it narrows, not by the identity of its
        // lists; see the note on it.
        if (next == _filter)
        {
            return;
        }

        var previous = _filter;
        var previousPage = _page;
        var previousKeepPager = _keepPager;

        _filter = next;

        // A refused date entry stops standing in for its field once any filter is applied, so
        // both fields show what the results are actually narrowed by.
        _refusedDates.Clear();
        _scopeLimitRefused = false;

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
            // The rows on screen are still the old selection's page, so filter and page roll back together,
            // or the buttons and the host's URL would describe a narrowing that never happened.
            _filter = previous;
            _page = previousPage;
            _keepPager = previousKeepPager;
        }

        // _filter and not next: what the host is told is what is in force, rolled back or not.
        await RaiseAsync(FilterChanged, _filter, Log);

        // Narrowing renumbers the pages, so a host mirroring this into a URL has to drop the page
        // it was holding. Same rule as the filter: whatever is in force, rolled back or not.
        await NotifyPageChangedAsync();
    }

    /// <summary>The catalogue's own words for the EHDS tokens the datakategori facet is made of.</summary>
    /// <remarks>
    /// Fetched rather than transcribed into <see cref="Texts"/>, which would drift. Empty until it
    /// lands, and for good if it fails, which costs the choices their words and nothing else.
    /// </remarks>
    private IReadOnlyDictionary<string, PropertyMetadataEntry> _vocabulary =
        new Dictionary<string, PropertyMetadataEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the vocabulary has been asked for, however that turned out.</summary>
    private bool _vocabularyAsked;

    /// <summary>The property key the datakategori tokens are defined under.</summary>
    private const string DataCategoryKey = "healthCategory";

    /// <summary>Fetch the vocabulary once, and only for a panel that has datakategorier to name.</summary>
    /// <remarks>
    /// An older API sends no datakategorier, and the call shares the search's rate limit. Asked at most
    /// once, failure included, rather than again on every keystroke.
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
        catch (Exception ex)
        {
            // Warning, not Error: the panel is not broken without it — every choice still filters
            // and the reader sees the token rather than the word. Deliberately silent on screen,
            // so the log line is the only trace a degraded label leaves.
            Log?.LogWarning(ex, "could not load the data-category vocabulary");
        }
    }

    // True only while a selection returns nothing: the controls are the reader's own, the counts
    // beside them are not ours to state. (Fhi.Metadata-v2bgr)
    private bool _facetsRetained;

    /// <summary>A count, or none while the counts on screen would describe another moment.</summary>
    private int? Counted(int count) => _facetsRetained ? null : count;

    /// <summary>Whether an answer offers nothing to choose from, in any facet.</summary>
    private static bool OffersNothing(FilterOptions facets) =>
        facets.KildeTyper.Count == 0 && facets.Kilder.Count == 0 && facets.Delkilder.Count == 0
        && facets.Variabelgrupper.Count == 0 && facets.Filters.Count == 0 && facets.DataTypes.Count == 0
        && facets.HelsefagligKodeverk.Count == 0 && facets.AdministrativtKodeverk.Count == 0
        && facets.Instruments.Count == 0 && facets.DataCategories.Count == 0;

    /// <summary>The answer to draw the panel from: the fresh one, unless it would leave the reader stranded.</summary>
    /// <remarks>
    /// A selection matching nothing empties every facet, removing the way to undo it. (Fhi.Metadata-v2bgr)
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
            // Reported, not swallowed — so nothing is logged here and the caller's catch is what
            // records it. Returning the empty answer would read as a panel with nothing to offer
            // when it is a request that failed. (Fhi.Metadata-v2bgr)
            _facetsRetained = false;

            throw;
        }
    }

    /// <summary>Refresh the facets and their counts for the current search and filter.</summary>
    /// <remarks>
    /// Its own request: counts move with search and filter, not page or order. A failure keeps the
    /// facets on screen, since stale numbers beat the panel emptying under a press.
    /// </remarks>
    private async Task FetchFacetsAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            // The API's own spelling of the resolved language, so a host's "en-GB" still gets English facets.
            // Norwegian goes as "nb", not "no": "no" has no parent culture for the API to fall back from.
            var language = ReaderLanguage.ForApi(Language);

            _facets = await RetainedAsync(
                await Client.GetFiltersAsync(_executedSearch, _filter, language), language);

            // The one place a new answer arrives, so the one place the tree built from the old one
            // has to go: identity alone would hold it were the previous instance ever handed back.
            (_treeOf, _kildeTree) = (null, null);

            _facetError = null;
            _retryFacetsEnabled = false;

            // After the facets, because whether it is worth asking at all depends on what they
            // hold. Its own failure is swallowed inside — the counts arriving is what this try
            // block reports on, and a missing label must not read as a missing facet.
            await EnsureCategoryWordsAsync();
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused the facet counts");

            // Sent beside every search, so a throttle shows in both regions at once and they must agree.
            _facetError = T.RateLimitError;

            // Offered no more here than beside the rows, and for the same reason: waiting is the
            // remedy, so a button saying otherwise would contradict the sentence it sits under.
            _retryFacetsEnabled = false;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not load the facets");

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
    /// Counts only: the rows are right, and re-fetching them would answer a different question. No
    /// arguments to capture, unlike <see cref="RetryRowsAsync"/>: the state still describes the screen.
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
