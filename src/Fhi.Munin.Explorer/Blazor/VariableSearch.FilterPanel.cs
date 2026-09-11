using System.Globalization;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The facet sidebar: what can be narrowed, and what narrowing it costs.</summary>
public partial class VariableSearch
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
    /// to report <c>Chosen</c> itself, or it would say nothing in the summary while narrowing.
    /// (Fhi.Metadata-uidue)
    /// </para>
    /// <para>
    /// <c>Searchable</c> gives the facet a box that narrows its own values. The search is the
    /// panel's alone — it never reaches the ticks — so such a facet reports <c>Chosen</c> over its
    /// whole list as well, or a chosen value typed out of sight would stop being counted while it
    /// is still narrowing.
    /// </para>
    /// <para>
    /// <c>NameInChips</c> is for a facet whose values do not say what they are away from their own
    /// heading — the catch-all's yes/no questions. A chip from one reads "Andre filtre: X".
    /// </para>
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
        bool NameInChips = false)
    {
        /// <summary>What is chosen in this facet: the summary's count, and the row of chips.</summary>
        /// <remarks>
        /// One projection for both, so the number on a folded facet and the chips over the results
        /// can never describe two different selections. (Fhi.Metadata-l9l2n.68)
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

    /// <summary>
    /// One value inside a facet, and the values nested under it.
    /// </summary>
    /// <remarks>
    /// <c>Count</c> is how many variables the value would leave, or null where there is no count to
    /// show. <c>Toggle</c> is what ticking it does, or null for a value that is not selectable —
    /// the kildetype headings the kilder are grouped under are labels rather than filters, because
    /// kildetype has a facet of its own.
    /// <para>
    /// <c>Collapsible</c> makes such a heading a disclosure of its own, so the reader lands on the
    /// three kildetype rows rather than on every kilde under them. Its <c>Count</c> is then how
    /// many kilder the group holds rather than how many variables a value would leave.
    /// </para>
    /// <para>
    /// <c>Language</c> is what <c>Label</c> is written in, and null means "do not mark this"
    /// rather than "mark it as the page's". Every value decides it, because nothing downstream
    /// can tell a catalogue name from prose this package composed.
    /// </para>
    /// </remarks>
    private sealed record FacetValue(
        string Key,
        string Label,
        string? Language,
        int? Count,
        bool Selected,
        Func<Task>? Toggle,
        IReadOnlyList<FacetValue> Children,
        bool Collapsible = false);

    /// <summary>A node on the way to becoming a <see cref="FacetValue"/> tree.</summary>
    /// <remarks>
    /// The delkilde, variabelgruppe and saved-filter facets all arrive as a flat list carrying a
    /// parent id, and all three become a tree the same way. This is the shape <see cref="Tree"/>
    /// works in so that rule lives in one place.
    /// </remarks>
    private sealed record TreeNode(Guid Id, Guid? ParentId, string Label, string? Language, int Count);

    /// <summary>A node whose label came out of the catalogue, carrying that label's language with it.</summary>
    private static TreeNode Node(Guid id, Guid? parentId, (string Text, string? Language) label, int count) =>
        new(id, parentId, label.Text, label.Language, count);

    /// <summary>
    /// What a facet value shows for a thing the catalogue may have left unnamed, and the language
    /// those words are in.
    /// </summary>
    /// <remarks>
    /// The name is the catalogue's own Norwegian; a code and <see cref="Texts.NotSpecified"/> are
    /// neither, and marking one of those hands a screen reader an identifier — or this package's
    /// prose — in a Norwegian voice, which is <c>lang</c> applied backwards (WCAG 3.1.2).
    /// </remarks>
    private (string Text, string? Language) CatalogueName(string? name, string? code)
    {
        var (text, norwegian) = T.Named(name, code);

        return (text, CatalogueProperties.Foreign(norwegian, Reader));
    }

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
    /// The catalogue's word for one EHDS token and the language it is written in, or the token
    /// itself where there is none.
    /// </summary>
    /// <remarks>
    /// The miss is shown rather than hidden, which is the rule <see cref="CatalogueProperties.Word"/>
    /// states: a facet drawing nothing for a token it cannot name would silently offer fewer
    /// choices than the catalogue has. A token is ugly and honest — and unmarked, a CURIE being
    /// prose in no language at all. An option the vocabulary lists but curated no label for is a
    /// miss on the same terms, and <see cref="CatalogueProperties.Option"/> is asked which it was
    /// rather than that being inferred here by comparing the label back against the token.
    /// </remarks>
    private (string Text, string? Language) CategoryWord(string value) =>
        _vocabulary.TryGetValue(DataCategoryKey, out var entry)
        && CatalogueProperties.Option(entry, value, Reader) is { Curated: true } word
            ? (word.Label, Foreign(word.Language))
            : (value, null);

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
    /// facet, so "01.01.2020" alone does not say which of them it is.
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
    /// Unmarked: the field's name is this package's word and the date is formatted for the reader,
    /// so both are already in the reader's own language.
    /// </remarks>
    private FacetValue DateValue(string key, string field, DateOnly date, Func<Task> clear) =>
        new(key,
            T.FilterInFacet(field, date.ToString("d", CatalogueProperties.Culture(Language))),
            Language: null,
            null,
            Selected: true,
            clear,
            []);

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
    /// <remarks>
    /// Closed at first paint like every facet but kilde: a panel that opens with everything
    /// expanded is what pushed the results off the screen. (Fhi.Metadata-l9l2n.67)
    /// </remarks>
    private FacetGroup KildeTypeGroup(FilterOptions facets) =>
        new("kildetype", T.FacetKildeType, OpenByDefault: false, [.. facets.KildeTyper.Select(KildeTypeValue)]);

    /// <summary>One kildetype, in the reader's own language whichever source names it.</summary>
    /// <remarks>
    /// So it is unmarked whichever way <see cref="Texts.KildeTypeNameFromApi"/> answers: the API
    /// resolves the word in the language this package asked in, the table under it is this
    /// package's own, and what is left is a bare enum token belonging to no language.
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
    /// The kilde facet: kilder grouped under their kildetype, each with its own delkilde and
    /// datasamling tree.
    /// </summary>
    /// <remarks>
    /// The whole tree is built from the facet payload alone — <see cref="DelkildeFacet"/> and
    /// <see cref="DatasamlingFacet"/> each carry the parents they hang under precisely so this
    /// needs no second request. The counts are the facet payload's own, which the API cross-filters
    /// like every other facet it answers — unlike the hierarchy endpoint's kilde totals, which is
    /// why the level is drawn from facets at all.
    /// </remarks>
    private FacetGroup KildeGroup(FilterOptions facets)
    {
        var levels = KildeLevels(facets);
        var kilder = VisibleKilder(facets, levels);

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
            .Select(group => KildeTypeHeading(facets, group, levels))
            .ToList();

        // A search that matches nothing has to leave the facet standing, or it would take the box
        // the reader must widen the term in away with it. Only while there is a term: the facet
        // still drops out when the API itself returned no kilder. (Fhi.Metadata-l9l2n.67)
        var empty = KildeSearchTerm is null ? null : T.FacetSearchNoMatch;

        // With one kildetype in the list its heading says nothing the facet above does not — and it
        // is exactly one whenever a kildetype has been chosen, which is when the panel is most
        // crowded. So the kilder are lifted out of it.
        var values = grouped.Count == 1 ? grouped[0].Children : grouped;

        return new FacetGroup(FacetName(HierarchyLevel.Kilde), T.FieldSource, OpenByDefault: true, values,
                              EmptyText: empty, Chosen: ChosenKilder(facets, levels),
                              Searchable: true);
    }

    /// <summary>Which of the three source levels are ticked, whatever the facet's own search is showing.</summary>
    /// <remarks>
    /// Read off the answer rather than off the values drawn, because a ticked kilde the search has
    /// hidden is still narrowing the results — and would otherwise lose its place in the summary's
    /// count and its chip over the results at once. (Fhi.Metadata-uidue) Reading the payload rather
    /// than the drawn tree, it collapses repeats itself, on the tree's rule — or a chip and its own
    /// checkbox could keep copies naming one delkilde two ways. (Fhi.Metadata-l9l2n.82)
    /// </remarks>
    private IReadOnlyList<FacetValue> ChosenKilder(FilterOptions facets, KildeLevelLookup levels) =>
    [
        .. ListedKilder(facets)
            .Where(kilde => _filter.KildeIds.Contains(kilde.Id))
            .Select(kilde => KildeValue(kilde)),
        .. ListedKilder(facets)
            .SelectMany(kilde => OnePerId(levels.Delkilder[kilde.Id],
                                          delkilde => delkilde.Id,
                                          delkilde => delkilde.ParentDelkildeId))
            .Where(delkilde => _filter.DelkildeIds.Contains(delkilde.Id))
            .Select(DelkildeValue),
        .. facets.Datasamlinger
            .Where(datasamling => _filter.DatasamlingIds.Contains(datasamling.Id))
            .Select(DatasamlingValue)
    ];

    /// <summary>The payload's kilder, an id it names more than once standing for one kilde.</summary>
    /// <remarks>
    /// Every reading of <see cref="FilterOptions.Kilder"/> that becomes markup goes through here:
    /// two entries with one id are two <c>&lt;li&gt;</c> siblings under the one key, and the
    /// renderer throws on the next diff rather than drawing it wrongly. (Fhi.Metadata-l9l2n.82)
    /// </remarks>
    private static IReadOnlyList<KildeFacet> ListedKilder(FilterOptions facets) =>
        [.. facets.Kilder.DistinctBy(kilde => kilde.Id)];

    /// <summary>What the reader has typed into the kilde facet's own search box.</summary>
    private string _kildeSearch = string.Empty;

    /// <summary>The same, as it counts: null for a box holding nothing that could narrow anything.</summary>
    private string? KildeSearchTerm =>
        string.IsNullOrWhiteSpace(_kildeSearch) ? null : _kildeSearch.Trim();

    /// <summary>The kilde facet's search box, named by a label of its own.</summary>
    private string KildeSearchId => $"munin-explorer-facet-search-{_instance}";

    /// <summary>The box itself, so focus can be put back on it before the values beside it are rewritten.</summary>
    private ElementReference _kildeSearchField;

    /// <summary>Record what was typed into the kilde facet's search box.</summary>
    /// <remarks>
    /// Focus first and the state after, because <c>onchange</c> fires <em>because</em> focus has
    /// left the box: a narrowing commit rewrites the list a reader who tabbed out is now standing
    /// in. (Fhi.Metadata-6we8a) Nothing here touches <see cref="_filter"/> — unticking a kilde the
    /// reader can no longer see would drop a choice they never released.
    /// </remarks>
    private async Task SearchKilderAsync(string? text)
    {
        if (RemovesDrawnKilder(text))
        {
            await _kildeSearchField.FocusAsync();
        }

        _kildeSearch = text ?? string.Empty;
    }

    /// <summary>Whether committing <paramref name="text"/> takes a kilde the panel is drawing off the screen.</summary>
    /// <remarks>
    /// The half of the rescue that says when there is anything to rescue focus from: a commit that
    /// widens the facet, or that leaves every drawn kilde standing, removed nothing, and the reader
    /// who blurred the box by clicking into something else is already there.
    /// </remarks>
    private bool RemovesDrawnKilder(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _facets is not { } facets)
        {
            return false;
        }

        var term = text.Trim();
        var levels = KildeLevels(facets);

        return VisibleKilder(facets, levels).Any(kilde => !KildeMatches(kilde, levels, term));
    }

    /// <summary>The kilder the facet's own search leaves, each with its whole tree under it.</summary>
    /// <remarks>
    /// The name of anything below a kilde counts as the kilde's own, or a reader typing a name they
    /// can see in the tree would empty the facet. <see cref="StringComparison.OrdinalIgnoreCase"/>,
    /// the comparison the kildeutforsker's facet search uses, so "does this text contain that text"
    /// means one thing.
    /// </remarks>
    private IReadOnlyList<KildeFacet> VisibleKilder(FilterOptions facets, KildeLevelLookup levels)
    {
        if (KildeSearchTerm is not { } term)
        {
            return ListedKilder(facets);
        }

        return [.. ListedKilder(facets).Where(kilde => KildeMatches(kilde, levels, term))];
    }

    /// <summary>Whether a kilde, or anything drawn under it, holds <paramref name="term"/>.</summary>
    /// <remarks>
    /// A datasamling is reached under its delkilde as well as straight off the kilde, since
    /// <see cref="KildeLevels"/> puts it in whichever of the two lookups its parent says.
    /// </remarks>
    private bool KildeMatches(KildeFacet kilde, KildeLevelLookup levels, string term) =>
        LabelMatches(T.Named(kilde.Name, kilde.ShortName).Text, term)
        || levels.DatasamlingerByKilde[kilde.Id].Any(
               datasamling => LabelMatches(DatasamlingLabel(datasamling).Text, term))
        || levels.Delkilder[kilde.Id].Any(
               delkilde => LabelMatches(DelkildeLabel(delkilde).Text, term)
                           || levels.DatasamlingerByDelkilde[delkilde.Id].Any(
                                  datasamling => LabelMatches(DatasamlingLabel(datasamling).Text, term)));

    private static bool LabelMatches(string label, string term) =>
        label.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>A kilde's kildetype, or the empty string when it has none — never null, so it can be a key.</summary>
    private static string KildeTypeKey(KildeFacet kilde) =>
        string.IsNullOrWhiteSpace(kilde.KildeType) ? "" : kilde.KildeType;

    /// <summary>A kildetype heading: a label rather than a filter, because kildetype has its own facet.</summary>
    /// <remarks>
    /// A disclosure of its own, so the panel opens on three group rows rather than on 46 kilder.
    /// Its count is how many kilder the group holds — what the row is hiding — rather than the
    /// variable count the values under it carry. (Fhi.Metadata-l9l2n.67)
    /// </remarks>
    private FacetValue KildeTypeHeading(
        FilterOptions facets,
        IGrouping<string, KildeFacet> kilder,
        KildeLevelLookup levels) =>
        new($"kildetype-group:{kilder.Key}",
            KildeTypeNameFromApi(facets, kilder.Key),
            Language: null,
            kilder.Count(),
            Selected: false,
            Toggle: null,
            [.. kilder.Select(kilde => KildeValue(kilde, levels))],
            Collapsible: true);

    private FacetValue KildeValue(KildeFacet kilde, KildeLevelLookup levels) =>
        KildeValue(kilde) with
        {
            Count = Counted(kilde.Count),
            Children = KildeChildren(kilde.Id, levels)
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
            []);
    }

    /// <summary>The two levels under a kilde, each hanging where its facet says it does.</summary>
    private IReadOnlyList<FacetValue> KildeChildren(Guid kildeId, KildeLevelLookup levels) =>
    [
        .. DatasamlingValues(levels.DatasamlingerByKilde[kildeId]),
        .. Tree(levels.Delkilder[kildeId]
                    .Select(delkilde => Node(
                        delkilde.Id, delkilde.ParentDelkildeId, DelkildeLabel(delkilde), delkilde.Count)),
                $"{FacetName(HierarchyLevel.Delkilde)}:",
                IsDelkildeChosen,
                ToggleDelkilde,
                Counted,
                delkildeId => DatasamlingValues(levels.DatasamlingerByDelkilde[delkildeId]))
    ];

    /// <summary>A delkilde on its own, on the same terms — the tree below builds it from the same parts.</summary>
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

    /// <summary>Datasamlinger as leaves: nothing in the catalogue hangs below one.</summary>
    private IReadOnlyList<FacetValue> DatasamlingValues(IEnumerable<DatasamlingFacet> datasamlinger) =>
    [
        .. datasamlinger.Select(datasamling =>
            DatasamlingValue(datasamling) with { Count = Counted(datasamling.Count) })
    ];

    /// <summary>A datasamling on its own — the same split the kilde and delkilde above are drawn through.</summary>
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
            []);
    }

    private (string Text, string? Language) DatasamlingLabel(DatasamlingFacet datasamling) =>
        CatalogueName(datasamling.Name, null);

    /// <summary>The delkilder and datasamlinger of the kilde facet, keyed by what each hangs under.</summary>
    /// <remarks>
    /// Datasamlinger are split across two lookups rather than keyed into one by whichever parent
    /// they hang from: the two id spaces are independent Guids off the wire, and one lookup would
    /// read a kilde id that happened to equal a delkilde id as the other level's key and misfile
    /// the row with no error anywhere.
    /// </remarks>
    private sealed record KildeLevelLookup(
        ILookup<Guid, DelkildeFacet> Delkilder,
        ILookup<Guid, DatasamlingFacet> DatasamlingerByKilde,
        ILookup<Guid, DatasamlingFacet> DatasamlingerByDelkilde);

    /// <summary>Both child levels of the kilde tree, in one pass over the facets.</summary>
    private static KildeLevelLookup KildeLevels(FilterOptions facets)
    {
        // GroupBy rather than ToDictionary: a payload repeating a delkilde id is malformed, but it
        // throws here on the render path and inside the kilde search box's onchange, either of
        // which tears the circuit down over what would otherwise be one oddly drawn row.
        var delkildeOwner = facets.Delkilder
            .GroupBy(delkilde => delkilde.Id)
            .ToDictionary(group => group.Key, group => group.First().KildeId);

        // A delkilde the payload left out — cross-filtered away, or belonging to another kilde — is
        // an absent parent, so its datasamlinger fall back to the kilde rather than disappearing
        // with it.
        bool HangsUnderItsDelkilde(DatasamlingFacet datasamling) =>
            datasamling.DelkildeId is { } parent
            && delkildeOwner.TryGetValue(parent, out var owner)
            && owner == datasamling.KildeId;

        return new KildeLevelLookup(
            facets.Delkilder.ToLookup(delkilde => delkilde.KildeId),
            facets.Datasamlinger
                .Where(datasamling => !HangsUnderItsDelkilde(datasamling))
                .ToLookup(datasamling => datasamling.KildeId),
            facets.Datasamlinger
                .Where(HangsUnderItsDelkilde)
                .ToLookup(datasamling => datasamling.DelkildeId!.Value));
    }

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
        new(FacetName(HierarchyLevel.Variabelgruppe),
            T.FieldVariableGroup,
            OpenByDefault: false,
            Tree(facets.Variabelgrupper
                     .Select(g => Node(g.Id, g.ParentId, CatalogueName(g.Name, null), g.Count)),
                 $"{FacetName(HierarchyLevel.Variabelgruppe)}:",
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
    /// Unmarked for the reason <see cref="KildeTypeValue"/> is: the API resolves the name in the
    /// language this package asked in, a legacy stored spelling is replaced out of this package's
    /// own table, and the fallback under both is that table again.
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
    /// AGENTS.md, "The API names a datatype, not this package". A facet carrying no name at all —
    /// an API predating them — falls back to the shipped table keyed by the code, because a button
    /// labelled with a blank string is an empty accessible name. (Fhi.Metadata-l9l2n.49)
    /// </remarks>
    private string DataTypeFacetLabel(DataTypeFacet dataType) =>
        T.NormalizeDataTypeDisplayName(dataType.DisplayName) is { } named && !string.IsNullOrWhiteSpace(named)
            ? named
            : T.DataTypeLabel(dataType.Value);

    private FacetGroup HelsefagligKodeverkGroup(FilterOptions facets) =>
        new("helsefaglig-kodeverk",
            T.FacetHelsefagligKodeverk,
            OpenByDefault: false,
            [.. facets.HelsefagligKodeverk.Select(HelsefagligKodeverkValue)]);

    /// <summary>One helsefaglig kodeverk, by the short name the catalogue keys it on.</summary>
    /// <remarks>
    /// Unmarked, on the terms <see cref="Texts.Named"/> already sets for a short name: this is the
    /// catalogue's key rather than its prose — the slot a kilde's own short name occupies, which
    /// that reading reports as not Norwegian — and the set is whatever V-HK holds. DÅR and HKR are
    /// Norwegian abbreviations, ICD-10 and NCMP-NCSP-NCRP are international tokens, and nothing
    /// here can tell which a given key is. <c>FullName</c> is the Norwegian half, and the panel
    /// does not draw it.
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
    /// recursing until the stack runs out.
    /// </para>
    /// <para>
    /// An id the payload names more than once is <see cref="OnePerId">collapsed to one node</see>
    /// before any of that, so where the value sits is the payload's meaning rather than its order.
    /// </para>
    /// <para>
    /// <c>under</c> takes values from another facet that belong beneath a node — the kilde tree's
    /// datasamlinger, which carry their own key prefix and their own selection, and so cannot be
    /// nodes here.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<FacetValue> Tree(
        IEnumerable<TreeNode> nodes,
        string keyPrefix,
        Func<Guid, bool> selected,
        Func<Guid, Func<Task>> toggle,
        Func<int, int?> count,
        Func<Guid, IReadOnlyList<FacetValue>>? under = null)
    {
        var all = OnePerId(nodes, node => node.Id, node => node.ParentId);

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

            // Same shape as AddRoots above, and for the same reason: each child is tested against a
            // set the recursion mutates, so building one sibling can place the next.
            List<FacetValue> children = under is null ? [] : [.. under(node.Id)];

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

    /// <summary>One entry per id, the copy hanging off a parent that is present winning.</summary>
    /// <remarks>
    /// Naming an id twice drew it twice, so one press ticked both and the row over the results
    /// carried two chips for one filter. Two entries with one id can differ in parent and in name
    /// alike, so keeping the first listed would nest, and label, by payload order. (Fhi.Metadata-l9l2n.82)
    /// </remarks>
    private static IReadOnlyList<T> OnePerId<T>(IEnumerable<T> entries, Func<T, Guid> id, Func<T, Guid?> parentId)
    {
        var listed = entries.ToList();
        var known = listed.Select(id).ToHashSet();

        return [.. listed.GroupBy(id).Select(copies => copies.FirstOrDefault(Parented) ?? copies.First())];

        bool Parented(T entry) => parentId(entry) is { } parent && known.Contains(parent);
    }

    /// <summary>Which way the last Utvid alle / Skjul alle press left every disclosure, if any.</summary>
    private bool? _foldAll;

    /// <summary>Bumped per press, and part of every disclosure's key, so the press rebuilds them.</summary>
    /// <remarks>
    /// A <c>&lt;details&gt;</c> holds its own open state in the DOM, so a facet the reader folded by
    /// hand no longer matches the <c>open</c> we rendered — and an unchanged value is never patched,
    /// which is exactly the press that has to land. A new key rebuilds instead of diffing.
    /// <para>
    /// NO TEST COVERS THIS, and none can: bUnit re-serialises the markup from the render tree, so a
    /// disclosure's <c>open</c> always equals what was last rendered and the divergence this defeats
    /// cannot be staged. Deleting it leaves the suite green and breaks a second press in a browser.
    /// Verified by hand on both sample hosts instead. (Fhi.Metadata-wcbxi)
    /// </para>
    /// </remarks>
    private int _foldGeneration;

    /// <summary>A disclosure's key: its facet, and the fold press it was last rebuilt for.</summary>
    /// <remarks>
    /// The facet half is what stops a facet the API drops from handing its open state to whichever
    /// facet takes its place. The generation is unchanged between presses, so a filter change still
    /// leaves open whatever the reader opened.
    /// </remarks>
    private string FacetKey(FacetGroup group) => $"{group.Key}#{_foldGeneration}";

    /// <summary>Whether a facet is drawn open: the last fold press, or the facet's own default.</summary>
    private bool FacetOpen(FacetGroup group) => _foldAll ?? group.OpenByDefault;

    /// <summary>Whether a kildetype group inside the kilde facet is drawn open.</summary>
    /// <remarks>
    /// Closed until a fold press says otherwise, which is what leaves the reader on the group rows.
    /// No generation of its own: a press rebuilds the facet above under a new key, and a keyed
    /// rebuild takes the whole subtree with it.
    /// </remarks>
    private bool GroupOpen => _foldAll ?? false;

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

    /// <summary>Open every facet at once, or fold every facet at once.</summary>
    /// <remarks>
    /// The rebuild costs the dataperiode's date fields whatever was typed into them but not yet
    /// committed — they bind on change, so a half-typed date lives only in the DOM. Accepted: the
    /// alternative is keying that one facet apart, and a press that skips a facet is worse.
    /// </remarks>
    private void FoldAll(bool open)
    {
        _foldAll = open;
        _foldGeneration++;
    }

    /// <summary>Whether the tree draws a guide line per level.</summary>
    /// <remarks>
    /// No initialiser on purpose: <see cref="LevelLines"/> is copied in here before the first
    /// render, so the parameter's default is the only place the resting state is written down.
    /// </remarks>
    private bool _levelLines;

    /// <summary>
    /// The panel's marker for the level lines, or null — an omitted attribute — while they are off.
    /// </summary>
    /// <remarks>
    /// A data attribute rather than a class name of this package's own. Not because a class would
    /// render badly — an unstyled class on the <c>&lt;ul&gt;</c> that is already there renders as it
    /// does today — but because a name is inventory: the README contract, the sample stylesheets and
    /// <c>assert-sample-css-in-step.sh</c> all have to carry it. A state marker owes nothing.
    /// (Fhi.Metadata-wcbxi)
    /// <para>
    /// This draws the lines; it says nothing about the control. What a screen reader hears is the
    /// switch's own <c>aria-checked</c>, always spelled out where this is omitted when off — two
    /// carriers for two audiences, one <c>_levelLines</c> behind both. (Fhi.Metadata-l9l2n.87)
    /// </para>
    /// </remarks>
    private string? LevelLinesMarker => _levelLines ? "true" : null;

    /// <summary>Turn the level lines on or off, and tell the host, so it can remember them.</summary>
    private Task ToggleLevelLinesAsync()
    {
        _levelLines = !_levelLines;

        return RaiseAsync(LevelLinesChanged, _levelLines, Log);
    }

    /// <summary>A facet's own label, saying how many of its values are chosen.</summary>
    /// <remarks>
    /// On the summary line, so a collapsed facet still says that something inside it is narrowing
    /// the list. Without it the only sign of a filter chosen three disclosures down is the number of
    /// results changing.
    /// </remarks>
    private static string GroupLabel(FacetGroup group) =>
        group.SelectedCount == 0 ? group.Label : $"{group.Label} ({group.SelectedCount})";

    /// <summary>A facet's values as a nested list of checkboxes.</summary>
    /// <remarks>
    /// Only the count carries a class, <c>munin-explorer-filters__count</c>, and it sits inside the
    /// label — see KildeSearch.Filters.cs. Keyed because counts reorder the values between
    /// renders, and an unkeyed patch would move the box under the reader's finger. (Fhi.Metadata-j0a2h)
    /// <para>
    /// <c>lang</c> goes on the <c>&lt;label&gt;</c>, which already holds the words, and never on
    /// the <c>&lt;li&gt;</c>, which also holds the values nested under this one: <c>lang</c>
    /// inherits, so a kilde's Norwegian would reach a child whose own
    /// <see cref="FacetValue.Language"/> is null and meant it. The kildeutforsker's panel marks
    /// its labels the same way, and a name marked in its chip but not on the checkbox that chip
    /// stands for would name one kilde two ways on one page.
    /// </para>
    /// </remarks>
    private RenderFragment FacetList(IReadOnlyList<FacetValue> values) => builder =>
    {
        builder.OpenElement(0, "ul");

        foreach (var value in values)
        {
            builder.OpenElement(1, "li");
            builder.SetKey(value.Key);

            if (value.Collapsible)
            {
                CollapsibleGroup(builder, value);
                builder.CloseElement();

                continue;
            }

            // Held in a local so the null check below is one the compiler can carry into the branch.
            var toggle = value.Toggle;

            if (toggle is null)
            {
                // Bare text, so unmarked: no value reaches here with a language of its own, and an
                // element to hang one on would be new structure in the panel — a munin-explorer
                // name and a Stiler rule for it — bought for a marking nothing asks for yet.
                builder.AddContent(2, value.Label);
            }
            else
            {
                builder.OpenElement(3, "label");
                builder.AddAttribute(4, "lang", value.Language);
                builder.OpenElement(5, "input");
                builder.AddAttribute(6, "type", "checkbox");
                builder.AddAttribute(7, "checked", value.Selected);

                // The event's own value is ignored: the toggle flips what the filter holds, which
                // is the one state a press and the render after it are certain to agree about.
                builder.AddAttribute(8, "onchange",
                                     EventCallback.Factory.Create<ChangeEventArgs>(this, _ => toggle()));

                // What a plain onchange does not do and this panel needs: a press that ApplyFilterAsync
                // drops mid-fetch writes no state, so the renders either side are equal and
                // the browser's own tick stays on over a filter that is off until `checked` is forced.
                builder.SetUpdatesAttributeName("checked");

                builder.CloseElement();
                builder.AddContent(9, value.Label);

                // The space is a text node of the label, not the span's first character: a name is
                // computed per element, so a space inside the span is trimmed off and the name
                // announces as "Dødsårsaksregisteret(30)".
                if (value.Count is { } count)
                {
                    builder.AddContent(10, " ");
                    builder.OpenElement(11, "span");
                    builder.AddAttribute(12, "class", "munin-explorer-filters__count");
                    builder.AddContent(13, $"({count})");
                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            if (value.Children.Count > 0)
            {
                builder.AddContent(14, FacetList(value.Children));
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    };

    /// <summary>One kildetype group inside the kilde facet, as a disclosure over its kilder.</summary>
    /// <remarks>
    /// A <c>&lt;details&gt;</c> and not a button with a chevron of its own: the marker, the open
    /// state and the focus ring are then the ones a host already draws for
    /// <c>.munin-explorer-filters summary</c>, so a group costs no class name and no new rule.
    /// <para>
    /// The count wears <c>munin-explorer-filters__groupcount</c> and not <c>__chosen</c>: these are
    /// group sizes, and nothing here is chosen. Stiler gives it <c>__chosen</c>'s own selector, so
    /// the tabular figures cannot drift apart from it. (Fhi.Metadata-l9l2n.104)
    /// </para>
    /// <para>
    /// No <c>lang</c>: the one value drawn here is <see cref="KildeTypeHeading"/>, whose words are
    /// the reader's own for the reason <see cref="KildeTypeValue"/> gives. The <c>&lt;summary&gt;</c>
    /// holds the count as well, so a marking put on it would reach that too — this package's own
    /// figure inside a foreign scope, which is the same defect one level down.
    /// </para>
    /// </remarks>
    private void CollapsibleGroup(RenderTreeBuilder builder, FacetValue value)
    {
        builder.OpenElement(15, "details");
        builder.AddAttribute(16, "open", GroupOpen);
        builder.OpenElement(17, "summary");
        builder.AddContent(18, value.Label);

        // The space is a text node of the summary rather than the span's first character, for the
        // reason the value counts further up are: a name is computed per element, so a space inside
        // the span is trimmed off and the group announces as "Biobank12".
        if (value.Count is { } members)
        {
            builder.AddContent(19, " ");
            builder.OpenElement(20, "span");
            builder.AddAttribute(21, "class", "munin-explorer-filters__groupcount");
            builder.AddContent(22, members.ToString(CultureInfo.CurrentCulture));
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.AddContent(23, FacetList(value.Children));
        builder.CloseElement();
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
    /// One at a time, because the API takes one, so ticking a second value unticks the first.
    /// Unticking the chosen one clears the facet — which a checkbox promises and a radio group
    /// denies, there being no "any kildetype" value to go back to.
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

    /// <summary>The chosen values as the row over the results draws them, in the panel's own order.</summary>
    /// <remarks>
    /// A projection of the facets and never a second collection beside them — see
    /// <see cref="ActiveFilters"/>. Every facet is walked rather than a known few named, so one
    /// added later draws chips unasked: a row covering some filters reads as covering all of them.
    /// A hierarchy value no facet named is spliced in where its own facet stands rather than
    /// appended after the walk, or one level would read as two filters at opposite ends of the row
    /// depending on which values the payload happened to name — see
    /// <see cref="UnfacetedHierarchyChips"/> for why such a value has a chip at all.
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
                    // A value nothing can untick has no removal to offer: the kildetype headings
                    // inside the kilde facet are labels rather than filters.
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

    /// <summary>
    /// Chips for the chosen kilder, delkilder, datasamlinger and variabelgrupper that the facets
    /// drew none for, at the levels <paramref name="drawnHere"/> admits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The facets are cross-filtered, so a value the reader chose can be absent from the payload
    /// describing what that selection leaves — the same gap <see cref="KildeName"/> has a fallback
    /// label for — and before the first answer, or after one that failed, there is no payload at
    /// all while a host may have mounted with <see cref="Filter"/> already set. Such a value
    /// narrows the results and had no chip, so with the trail's own × gone the only control left
    /// was "Fjern alle filtre", which drops the datatype and the dates with it. (Fhi.Metadata-oj286)
    /// </para>
    /// <para>
    /// Keyed off what the walk above actually drew rather than off the payload a second time, so
    /// the two can never disagree about which values already have a control: <c>removable</c> holds
    /// the <see cref="FacetValue.Key"/> of every chip, and every such key comes from
    /// <see cref="FacetName"/> — through <see cref="FacetValueKey"/> here and as
    /// <see cref="Tree"/>'s prefix there. The levels themselves are <see cref="HierarchyLevels"/>,
    /// the list the trail is drawn from, so neither reading can grow a level the other has not.
    /// </para>
    /// <para>
    /// Values one level cannot name at all collapse into a single chip carrying their count, the
    /// way a trail step does it: two chips reading "Datasamling" are two controls a screen reader
    /// cannot tell apart, and removing one would leave one named identically behind, so pressing
    /// it would read as having done nothing. Such a press takes all of them off, and never a value
    /// the row has already named on its own.
    /// </para>
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
    /// Both halves in one reading, because <c>NameInChips</c> settles both: a facet whose heading
    /// goes into the chip composes this package's prose around the value, and what comes out is
    /// this package's own words in the reader's language whatever the value alone was written in —
    /// the same reading <see cref="DateValue"/> applies to the text it composes for itself. Decided
    /// beside the composition rather than as an override of <see cref="FacetValue.Language"/>, so a
    /// facet cannot be given one half and left with the other.
    /// </remarks>
    private (string Text, string? Language) ChipReading(FacetGroup group, FacetValue value) =>
        group.NameInChips
            ? (T.FilterInFacet(group.Label, value.Label), null)
            : (value.Label, value.Language);

    /// <summary>Untick one value from the chip row, through the state its own checkbox writes.</summary>
    /// <remarks>
    /// Focus first: the pressed control leaves as it acts, and the last chip takes the whole row
    /// with it, so focus would otherwise fall to <c>&lt;body&gt;</c>. The search field is the one
    /// control above the row that is there whether a filter is left or not. (Fhi.Metadata-ag4n7)
    /// </remarks>
    private async Task RemoveFilterAsync(Func<Task> toggle)
    {
        await _searchField.FocusAsync();

        await toggle();
    }

    /// <summary>Drop every filter and fetch the whole search again.</summary>
    /// <remarks>
    /// The one control that offers this, and it stands in the chip row rather than at the foot of
    /// the panel — so it is gone the moment it lands, and focus moves ahead of it for the reason
    /// <see cref="RemoveFilterAsync"/> gives. (Fhi.Metadata-l9l2n.68)
    /// </remarks>
    private async Task ClearFiltersAsync()
    {
        await _searchField.FocusAsync();

        await ApplyFilterAsync(VariableFilter.None);
    }

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
        await RaiseAsync(FilterChanged, _filter, Log);

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
            // Reported, not swallowed — so nothing is logged here and the caller's catch is what
            // records it. Returning the empty answer would read as a panel with nothing to offer
            // when it is a request that failed. (Fhi.Metadata-v2bgr)
            _facetsRetained = false;

            throw;
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
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused the facet counts");

            // This refresh goes out alongside every search, so a throttled reader meets this panel
            // and the result list in the same render. "The counts may be out of date" beside "you
            // have made too many requests" would have the two regions disagree about what happened,
            // and only one of them would be telling the reader what to do about it.
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
