using System.Text.Json;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Kelda's facets: what the list can be narrowed by, counted off the list itself.</summary>
/// <remarks>
/// <para>
/// Four facets, in the order Munin's own Kelda draws them: kildetype, kategori, tilgangsnivå and
/// databehandler. Every one of them is computed and applied <em>client-side</em>, over the list
/// this component fetched once — see the remarks on the class. <c>kildeType</c> is a parameter the
/// endpoint takes, so that one facet could have gone to the server, and deliberately does not: two
/// facets behaving differently is a difference the reader can feel and nobody can explain, and a
/// server-side kildetype would fetch a narrower list that the other three would then count over.
/// </para>
/// <para>
/// The counts are therefore <em>not</em> cross-filtered, which is the same thing said from the
/// other side: an option's number is how many kilder in the whole catalogue carry that value, not
/// how many the current selection would leave. Runa's are cross-filtered because its facets come
/// from an endpoint that recounts them per request; this list is fetched once and never refetched,
/// so there is nothing to recount against. The options are built off <see cref="_kilder"/> rather
/// than off the filtered list for a second reason as well: an option that vanished the moment it
/// stopped matching would take the checkbox the reader is standing on out of the DOM with it.
/// </para>
/// <para>
/// A facet with no options is not rendered at all — no heading, no container, nothing. Munin's
/// Kelda renders Kategori as an empty heading with no choices under it, which reads as a broken
/// panel rather than as a catalogue that has not filled the field in. Leaving the facet out makes
/// "is the data there?" a question about the data, whose answer this component is right either way.
/// </para>
/// <para>
/// Selecting is OR within a facet and AND across them, which is what a reader expects of
/// checkboxes and is the one rule an implementation gets wrong silently: an AND within the facet
/// answers two ticked boxes with an empty list, and an empty list reads as "no matches" rather
/// than as a bug.
/// </para>
/// <para>
/// Nothing here repairs the catalogue. Databehandler is free text and arrives as 39 variants on a
/// live catalogue, one of them a 200-character sentence and four of them different spellings of
/// Folkehelseinstituttet; the facet shows them as they are, truncated for display with the whole
/// value in <c>title</c>. Merging variants would mean this component deciding that two strings name
/// one organisation, which is a claim about the catalogue that belongs in the catalogue —
/// <c>Fhi.Metadata-4kxfv</c>. Fix it there and this facet improves without being touched.
/// </para>
/// <para>
/// A facet past <see cref="FacetLimits.FacetSearchThreshold"/> values gets a search box of its
/// own, which narrows the values it draws and nothing else: not the list, not the counts, not the
/// other facets, and not <see cref="_chosen"/> — a value ticked and then typed out of sight is
/// still ticked and still narrowing, which is what makes the box safe to use halfway through
/// choosing. It is emphatically not the merge above by another route: typing <c>folke</c> brings
/// the three spellings that contain those letters together on screen and leaves them three
/// separate choices, while the fourth, spelled <c>FHI</c>, does not match at all — which is the
/// catalogue's state shown rather than tidied.
/// </para>
/// <para>
/// That same number caps what such a facet draws at rest: the first ten values, plus any ticked
/// one past them, with the remainder behind a "Vis N til" the reader can press. The cap is off
/// entirely while the box holds a term, so searching and hiding cannot fight over one value.
/// </para>
/// </remarks>
public sealed partial class KildeSearch
{
    /// <summary>How much of a facet label is drawn before it is cut short.</summary>
    /// <remarks>
    /// Cut in C# because the package ships no CSS, and a host with no rule would lay out a 200-character databehandler.
    /// Cosmetic only: the whole value is on the <c>title</c>, and the checkbox filters on it uncut.
    /// </remarks>
    private const int FacetLabelLimit = 60;

    /// <summary>The key of the additional property holding a kilde's EHDS categories.</summary>
    private const string CategoryKey = "healthCategory";

    /// <summary>The key of the additional property holding a kilde's access-rights token.</summary>
    private const string AccessRightsKey = "accessRights";

    /// <summary>One facet as the panel defines it: where its values come from, and how each is worded.</summary>
    /// <param name="Key">Stable across renders, so a selection belongs to one facet and to no other.</param>
    /// <param name="Heading">The facet's own heading, in the reader's language.</param>
    /// <param name="Values">None, one or several: kategori is a list, and keeping only its first drops kilder.</param>
    /// <param name="Label">
    /// A value as words, with their language: kildetype falls back to catalogue text, kategori and tilgangsnivå to a
    /// CURIE in no language, which <c>lang="no"</c> would misread (WCAG 3.1.2). Databehandler's are the catalogue's.
    /// </param>
    private sealed record FacetDefinition(
        string Key,
        string Heading,
        Func<KildeSummary, IReadOnlyList<string>> Values,
        Func<string, FacetLabel> Label);

    /// <summary>What a choice is called, and the language those words are in.</summary>
    /// <param name="Text">The value as a reader should see it.</param>
    /// <param name="Language">
    /// Null where <paramref name="Text"/> is the reader's own language or an identifier in none. Null means
    /// "do not mark this", which is not the same as "mark it as the page's language".
    /// </param>
    private readonly record struct FacetLabel(string Text, string? Language);

    /// <summary>A facet as the panel draws it: a disclosure holding a heading and the choices under it.</summary>
    /// <remarks>
    /// Only the first is <c>OpenByDefault</c>, which seeds the disclosure; the fold is the reader's until a rebuild.
    /// All open runs long (databehandler alone can run to dozens); all shut hides that there is anything inside.
    /// </remarks>
    private sealed record Facet(
        string Key, string Heading, IReadOnlyList<FacetOption> Options, bool OpenByDefault = false);

    /// <summary>One choice in a facet: a value, its words, and how many kilder carry it.</summary>
    /// <param name="Value">The catalogue's value, matched whole: two prefixes over one token are two values.</param>
    /// <param name="Label">The value as a reader should see it, at whatever length the catalogue wrote it.</param>
    /// <param name="Count">Over the whole list, so not cross-filtered as Runa's is; see the class remarks.</param>
    /// <param name="Language">
    /// The catalogue's language where <paramref name="Label"/> is the catalogue's words. Null both for this package's
    /// wording and for a CURIE in no language: null means "do not mark this". See <see cref="Option"/>.
    /// </param>
    private sealed record FacetOption(string Value, string Label, int Count, string? Language)
    {
        /// <summary>The choice's visible text, cut to length, without its count.</summary>
        public string Text => Shorten(Label);

        /// <summary>The count as it is drawn: the number in parentheses, and no space.</summary>
        /// <remarks>
        /// The separating space is emitted beside this element, so a host rule dresses the number and not the gap;
        /// browsers keep the space in the accessible name. (Fhi.Metadata-cgk85, Fhi.Metadata-47lha)
        /// </remarks>
        public string CountText => $"({Count})";

        /// <summary>
        /// The whole label for a choice whose text was cut, and nothing at all for one that was not.
        /// </summary>
        /// <remarks>
        /// Only where it says something: a <c>title</c> repeating text already on screen is read out
        /// twice by some screen readers and hovers a tooltip over every option for no reason.
        /// </remarks>
        public string? Title => Label.Length > FacetLabelLimit ? Label : null;
    }

    /// <summary>
    /// Which values are ticked, per facet. A facet missing from here, or holding an empty set, is
    /// one nothing is chosen in — which is no constraint rather than an impossible one.
    /// </summary>
    /// <remarks>
    /// Ordinal: a case-insensitive set would fold two catalogue values into one and tick both boxes from one click.
    /// </remarks>
    private readonly Dictionary<string, HashSet<string>> _chosen = new(StringComparer.Ordinal);

    /// <summary>
    /// The values ticked in each facet when the list opens, keyed by one of <see cref="FacetKeys"/>.
    /// Set by the host, typically from its own URL; the component owns the choice afterwards.
    /// </summary>
    /// <remarks>
    /// Read once, on initialisation, as <see cref="Search"/> is. A key outside
    /// <see cref="FacetKeys"/> and an empty value are dropped. A value no kilde carries is kept, and
    /// narrows the list to nothing, as a ticked value that stopped matching does.
    /// </remarks>
    [Parameter] public IReadOnlyDictionary<string, IReadOnlyList<string>>? FacetChoices { get; set; }

    /// <summary>
    /// Raised on every tick, untick and clear in the facet panel or the chip row, with every facet
    /// that has a value ticked — keyed by <see cref="FacetKeys"/>, in that order, and empty when
    /// nothing is. Gives a host <c>@bind-FacetChoices</c>.
    /// </summary>
    [Parameter] public EventCallback<IReadOnlyDictionary<string, IReadOnlyList<string>>> FacetChoicesChanged { get; set; }

    /// <summary>
    /// The four facets' keys, in the order the panel draws them: <c>kildetype</c>, <c>kategori</c>,
    /// <c>tilgangsniva</c> and <c>databehandler</c> — the names Munin's own Kelda gives the same
    /// four in its address.
    /// </summary>
    public static IReadOnlyList<string> FacetKeys { get; } =
        [KildetypeFacet, CategoryFacet, AccessLevelFacet, DataProcessorFacet];

    private const string KildetypeFacet = "kildetype";
    private const string CategoryFacet = "kategori";
    private const string AccessLevelFacet = "tilgangsniva";
    private const string DataProcessorFacet = "databehandler";

    /// <summary>What has been typed into each facet's own value search, exactly as the reader typed it.</summary>
    /// <remarks>
    /// Never read by <see cref="MatchesFacets"/>, unlike <see cref="_chosen"/>: it narrows only what the panel draws.
    /// Held raw, so the field renders back what was typed; only <see cref="FacetSearchTerm"/> decides what counts.
    /// </remarks>
    private readonly Dictionary<string, string> _facetSearch = new(StringComparer.Ordinal);

    /// <summary>Each facet's search field, so focus can be put back on it before its values are rewritten.</summary>
    /// <remarks>
    /// Written by <c>@ref</c>; a facet that lost its box keeps a stale reference nothing reads, since only that facet's
    /// own commit focuses it, and the field is still on screen then.
    /// </remarks>
    private readonly Dictionary<string, ElementReference> _facetSearchFields = new(StringComparer.Ordinal);

    /// <summary>Which facets the reader has asked to see the whole of, and how long each was when they asked.</summary>
    /// <remarks>
    /// Keyed on the facet, not its disclosure: folding a facet is not a decision to hide its values again.
    /// The length feeds <see cref="FacetLimits.StillExpanded"/>, though this list is fetched once, so all panels agree.
    /// </remarks>
    private readonly Dictionary<string, int> _expandedFacets = new(StringComparer.Ordinal);

    /// <summary>Whether the panel is unfolded; the markup says why the reader still sees it while folded.</summary>
    private bool _filtersOpen;

    /// <summary>The four definitions, built once; <see cref="Definitions"/> says why they are held.</summary>
    private IReadOnlyList<FacetDefinition>? _definitions;

    /// <summary>The reader <see cref="_definitions"/> was built for, so a change of language rebuilds them.</summary>
    private string? _definitionsReader;

    private string FacetsId => $"munin-explorer-filters-{_instance}";

    /// <summary>The id joining one facet's search field to its own label.</summary>
    /// <remarks>
    /// The facet key as well as the instance: two mounts on one page must not collide, and neither
    /// must two facets inside one mount.
    /// </remarks>
    private string FacetSearchId(string key) => $"munin-explorer-facet-search-{_instance}-{key}";

    /// <summary>The panel heading's level: one below the component's title, so the outline stays unbroken.</summary>
    /// <remarks>
    /// The level an open kilde's name gets too, without a clash: the panel is drawn in the list branch and the kilde
    /// in the drilldown, so the two are never on screen together.
    /// </remarks>
    private int FilterLevel => Math.Clamp(TitleLevel + 1, 1, 6);

    /// <summary>A facet heading's level: one below the panel's own heading.</summary>
    private int FacetLevel => Math.Clamp(FilterLevel + 1, 1, 6);

    /// <summary>The four facets' definitions, in the order the panel draws them.</summary>
    /// <remarks>
    /// Held because <see cref="MatchesFacets"/> reads them per kilde, and a rebuild allocates eight closures per row.
    /// Only headings go stale (labels read <c>T</c> when called), so a change of <see cref="Language"/> rebuilds them.
    /// </remarks>
    private IReadOnlyList<FacetDefinition> Definitions
    {
        get
        {
            if (_definitions is not null && string.Equals(_definitionsReader, Reader, StringComparison.Ordinal))
            {
                return _definitions;
            }

            _definitionsReader = Reader;

            return _definitions =
            [
                new(KildetypeFacet, T.ColumnKildetype,
                    kilde => One(kilde.Kildetype), value => Translated(T.KildeTypeLabel(value, value), value)),
                new(CategoryFacet, T.FacetCategory, Categories, value => Vocabulary(CategoryKey, value)),
                new(AccessLevelFacet, T.FacetAccessLevel,
                    kilde => One(Property(kilde, AccessRightsKey)), value => Vocabulary(AccessRightsKey, value)),

                // Free text, so there is nothing to look it up in: the value is the catalogue's own word, always,
                // which is why it says so rather than asking whether some lookup missed.
                new(DataProcessorFacet, T.FieldDataProcessor,
                    kilde => One(kilde.DataProcessor), value => new FacetLabel(value, "no"))
            ];
        }
    }

    /// <summary>The facets worth drawing, counted over the whole list.</summary>
    /// <remarks>
    /// Built per render, as the variable explorer's are, so a facet and the rows beside it cannot describe two moments.
    /// <c>OpenByDefault</c> is set after empty facets drop, so a catalogue with no kildetype still opens one.
    /// </remarks>
    private IReadOnlyList<Facet> Facets =>
    [
        .. Definitions
            .Select(Build)
            .Where(facet => facet.Options.Count > 0)
            .Select((facet, index) => facet with { OpenByDefault = index == 0 })
    ];

    /// <summary>How many values are ticked across every facet — what the folded panel is hiding.</summary>
    private int ChosenCount => _chosen.Values.Sum(values => values.Count);

    /// <summary>How many values are ticked in one facet — what a folded facet is hiding.</summary>
    private int ChosenIn(string key) => _chosen.TryGetValue(key, out var values) ? values.Count : 0;

    /// <summary>One facet, counted.</summary>
    /// <remarks>
    /// Distinct per kilde, so a kategori listed twice counts its kilde once. Ordered by label in catalogue collation
    /// (Norwegian names whoever reads, so æ, ø and å sort last), then by value, so equal labels keep a stable order.
    /// </remarks>
    private Facet Build(FacetDefinition definition)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var kilde in _kilder)
        {
            foreach (var value in definition.Values(kilde).Distinct(StringComparer.Ordinal))
            {
                counts[value] = counts.GetValueOrDefault(value) + 1;
            }
        }

        var options = counts
            .Select(entry => Option(definition, entry.Key, entry.Value))
            .OrderBy(option => option.Label, CatalogueProperties.CatalogueOrder)
            .ThenBy(option => option.Value, StringComparer.Ordinal)
            .ToList();

        return new Facet(definition.Key, definition.Heading, options);
    }

    /// <summary>One value of a facet as a choice: its words, their language, and its count.</summary>
    /// <remarks>
    /// Catalogue words carry the catalogue's language, or a Norwegian name is read with English phonetics (WCAG 3.1.2).
    /// A CURIE such as <c>eu-access:OP_DATPRO</c> stays unmarked, and a language the reader already reads is dropped.
    /// </remarks>
    private FacetOption Option(FacetDefinition definition, string value, int count)
    {
        var (label, language) = definition.Label(value);

        return new FacetOption(
            value, label, count, language is null ? null : CatalogueProperties.Foreign(language, Reader));
    }

    /// <summary>A label this package translated, or the catalogue's own text where the translation missed.</summary>
    /// <remarks>
    /// Judged by the answer, not the value: a translation that came back as the value is the fallback, for kildetype a
    /// Munin enum member such as <c>noeHeltNytt</c>, which reads as Norwegian.
    /// </remarks>
    private static FacetLabel Translated(string label, string value) =>
        new(label, string.Equals(label, value, StringComparison.Ordinal) ? "no" : null);

    /// <summary>A token in the words of the API's vocabulary, as the detail panel words it.</summary>
    /// <remarks>
    /// A token with no curated word keeps its checkbox, whole and unmarked: dropping it would hide kilder silently.
    /// <c>Curated</c> decides, not a label-equals-value test, which misreads case (Fhi.Metadata-o49mx).
    /// </remarks>
    private FacetLabel Vocabulary(string key, string value) =>
        _vocabulary.TryGetValue(key, out var entry)
        && CatalogueProperties.Option(entry, value, Reader) is { Curated: true } word
            ? new FacetLabel(word.Label, word.Language)
            : new FacetLabel(value, null);

    /// <summary>Whether <paramref name="kilde"/> survives every facet the reader has chosen in.</summary>
    /// <remarks>
    /// OR within a facet, hence a set of values, and AND across facets, so two facets narrow rather than widen.
    /// </remarks>
    private bool MatchesFacets(KildeSummary kilde)
    {
        foreach (var definition in Definitions)
        {
            if (!_chosen.TryGetValue(definition.Key, out var chosen) || chosen.Count == 0)
            {
                continue;
            }

            if (!definition.Values(kilde).Any(chosen.Contains))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsChosen(string key, string value) =>
        _chosen.TryGetValue(key, out var chosen) && chosen.Contains(value);

    /// <summary>Tick or untick one value.</summary>
    /// <remarks>
    /// No request behind it and nothing to await: the list is already in hand, so the render that
    /// follows the handler is the whole of what changes.
    /// </remarks>
    private async Task ChooseAsync(string key, string value, bool chosen)
    {
        Choose(key, value, chosen);

        await RaiseFacetChoicesAsync();
    }

    private void Choose(string key, string value, bool chosen)
    {
        if (!_chosen.TryGetValue(key, out var values))
        {
            values = new HashSet<string>(StringComparer.Ordinal);
            _chosen[key] = values;
        }

        if (chosen)
        {
            values.Add(value);
        }
        else
        {
            values.Remove(value);
        }
    }

    /// <summary>Untick one value from the chip row, through the state the checkbox writes.</summary>
    /// <remarks>
    /// Only through <see cref="Choose"/>: a chip clearing its value any other way would leave the checkbox ticked
    /// over a list that had stopped obeying it.
    /// </remarks>
    private async Task RemoveFilterAsync(string key, string value)
    {
        await RescueFocusAsync();

        await ChooseAsync(key, value, false);
    }

    /// <summary>Untick every value in every facet, in one write of that same state.</summary>
    /// <remarks>
    /// Emptying <see cref="_chosen"/> is the write <see cref="Choose"/> makes, for every value at once; a walk over the
    /// chips would leave ticked whatever the row is not drawing.
    /// </remarks>
    private async Task ClearFacetsAsync()
    {
        await RescueFocusAsync();

        _chosen.Clear();

        await RaiseFacetChoicesAsync();
    }

    /// <summary>Take <see cref="FacetChoices"/> into <see cref="_chosen"/>, keeping what names a facet.</summary>
    private void SeedFacetChoices()
    {
        foreach (var (key, values) in FacetChoices ?? new Dictionary<string, IReadOnlyList<string>>())
        {
            if (FacetKeys.FirstOrDefault(facet => string.Equals(facet, key, StringComparison.OrdinalIgnoreCase))
                is not { } facetKey)
            {
                continue;
            }

            foreach (var value in values.Where(value => !string.IsNullOrEmpty(value)))
            {
                Choose(facetKey, value, true);
            }
        }
    }

    /// <summary>What <see cref="FacetChoicesChanged"/> carries: the ticked values, facet by facet.</summary>
    private Task RaiseFacetChoicesAsync()
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> choices = FacetKeys
            .Where(key => ChosenIn(key) > 0)
            .ToDictionary(key => key, key => (IReadOnlyList<string>)[.. _chosen[key]], StringComparer.Ordinal);

        return RaiseAsync(FacetChoicesChanged, choices, Log);
    }

    /// <summary>Hand focus to the search field before the pressed control leaves the page.</summary>
    /// <remarks>
    /// The pressed control leaves as it acts, the last chip taking the row, so focus would land on <c>&lt;body&gt;</c>.
    /// The field, not a neighbouring chip: it is the one control above the row always there. (Fhi.Metadata-ag4n7)
    /// </remarks>
    private ValueTask RescueFocusAsync() => _searchField.FocusAsync();

    /// <summary>The ticked values as the row over the results draws them, in the panel's own order.</summary>
    /// <remarks>
    /// A projection of <see cref="_chosen"/>, never a second collection; see <see cref="ActiveFilters"/>.
    /// Sorted on the facet list's own two keys, so re-sorting the panel is visibly two edits.
    /// </remarks>
    private IReadOnlyList<ActiveFilters.Chip> ActiveFilterChips
    {
        get
        {
            List<ActiveFilters.Chip> chips = [];

            foreach (var definition in Definitions)
            {
                if (!_chosen.TryGetValue(definition.Key, out var values) || values.Count == 0)
                {
                    continue;
                }

                var key = definition.Key;

                // Counted as nought because a chip draws no count. Option is reused all the same:
                // it decides a value's words, its cut and its lang, and a second reading of those
                // is a chip and a checkbox naming one value two ways.
                chips.AddRange(values
                    .Select(value => Option(definition, value, 0))
                    .OrderBy(option => option.Label, CatalogueProperties.CatalogueOrder)
                    .ThenBy(option => option.Value, StringComparer.Ordinal)
                    .Select(option => new ActiveFilters.Chip(
                        option.Text,
                        T.RemoveFilter(option.Text),
                        option.Title,
                        option.Language,
                        () => RemoveFilterAsync(key, option.Value))));
            }

            return chips;
        }
    }

    // The standing half of the question this component's own chevron asks, and all of it that
    // applies: the panel keeps no press, so RowPress's drag clause cannot be read here.
    // (Fhi.Metadata-zel47)
    private void ToggleFiltersFromControl(MouseEventArgs released)
    {
        if (!RowPress.WasSelectionStandingStill(released))
        {
            _filtersOpen = !_filtersOpen;
        }
    }

    /// <summary>Which way the last Utvid alle / Skjul alle press left every facet, if any.</summary>
    private bool? _foldAll;

    /// <summary>Bumped per press, and part of every disclosure's key, so the press rebuilds them.</summary>
    /// <remarks>
    /// A hand-folded <c>&lt;details&gt;</c> no longer matches the <c>open</c> last rendered, and an unchanged value
    /// is never patched. No test can stage that; the same field on <see cref="VariableSearch"/> says why.
    /// </remarks>
    private int _foldGeneration;

    /// <summary>A disclosure's key: its facet, and the fold press it was last rebuilt for.</summary>
    /// <remarks>
    /// The generation is unchanged between presses, so ticking a value still leaves open whatever
    /// the reader opened — and folded whatever they folded after Utvid alle.
    /// </remarks>
    private string FacetKey(Facet facet) => $"{facet.Key}#{_foldGeneration}";

    /// <summary>Whether a facet is drawn open: the last fold press, or the facet's own default.</summary>
    private bool FacetOpen(Facet facet) => _foldAll ?? facet.OpenByDefault;

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
    /// The rebuild loses a term typed into a facet's search field but not committed: the boxes commit on change, so a
    /// half-typed term lives only in the DOM. Committed terms are this component's own and survive.
    /// </remarks>
    private void FoldAll(bool open)
    {
        _foldAll = open;
        _foldGeneration++;

        FoldAllFacetValues(open);
    }

    /// <summary>Whether <paramref name="facet"/> is long enough to be given a search box — and a cap.</summary>
    /// <remarks>
    /// One <see cref="FacetLimits"/> threshold decides both, in all three panels, so "long" cannot mean two things.
    /// It is counted over the whole list, so neither control comes and goes under the reader's hand as they tick.
    /// </remarks>
    private static bool IsSearchable(Facet facet) => FacetLimits.IsLong(facet.Options.Count);

    /// <summary>What is in one facet's search field, as the reader typed it.</summary>
    private string FacetSearchValue(string key) =>
        _facetSearch.TryGetValue(key, out var text) ? text : string.Empty;

    /// <summary>The same, as it counts: null for a field that is empty or holds only spaces.</summary>
    /// <remarks>
    /// One definition of "there is a search here", as <c>SearchText</c> is over the list, so none disagree on spaces.
    /// <see cref="AsTerm"/> holds it, as <see cref="RemovesDrawnOptions"/> asks it of a term not yet committed.
    /// </remarks>
    private string? FacetSearchTerm(string key) => AsTerm(FacetSearchValue(key));

    /// <summary>What a field holds, as a search: null where it holds nothing that could narrow anything.</summary>
    private static string? AsTerm(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Commit a facet's search term, rescuing focus only when the render removes what it stands on.</summary>
    /// <remarks>
    /// Focus first and state after, guarded as <see cref="ClearSearchAndRefocusAsync"/> is: <c>onchange</c> is a blur.
    /// <see cref="_chosen"/> is untouched: unticking a value out of sight would drop a filter never released.
    /// </remarks>
    private async Task SearchFacetAsync(string key, string? text)
    {
        if (RemovesDrawnOptions(key, text) && _facetSearchFields.TryGetValue(key, out var field))
        {
            await field.FocusAsync();
        }

        _facetSearch[key] = text ?? string.Empty;
    }

    /// <summary>Whether committing <paramref name="text"/> takes a drawn option off the screen.</summary>
    /// <remarks>
    /// A commit that widens the facet, or leaves every drawn option standing, removed nothing, so there is no focus to
    /// rescue: the reader who blurred the box by clicking into another is already typing there. (Fhi.Metadata-6we8a)
    /// </remarks>
    private bool RemovesDrawnOptions(string key, string? text)
    {
        if (AsTerm(text) is not { } term ||
            Definitions.FirstOrDefault(definition => definition.Key == key) is not { } definition)
        {
            return false;
        }

        return VisibleOptions(Build(definition)).Visible.Any(option => !Matches(option, term));
    }

    /// <summary>What a facet draws: every match while it is searched, its capped values otherwise.</summary>
    /// <remarks>
    /// Matched on the whole label, case-blind as the list search is, never folding spellings (Fhi.Metadata-4kxfv).
    /// A term lifts the cap so no match hides behind it; <see cref="CappedValues{T}"/> says why one pass yields both.
    /// </remarks>
    private CappedValues<FacetOption> VisibleOptions(Facet facet)
    {
        if (FacetSearchTerm(facet.Key) is { } term)
        {
            return FacetLimits.Uncapped<FacetOption>([.. facet.Options.Where(option => Matches(option, term))]);
        }

        return FacetLimits.Cap(
            facet.Options,
            IsFacetExpanded(facet),
            option => IsChosen(facet.Key, option.Value));
    }

    /// <summary>Lift every facet's cap at once, or put all of them back.</summary>
    /// <remarks>
    /// Utvid alle has to reach past the cap as well as past the fold, or the control that offers to
    /// open everything stops ten values into a facet and says nothing about it.
    /// </remarks>
    private void FoldAllFacetValues(bool open)
    {
        _expandedFacets.Clear();

        if (!open)
        {
            return;
        }

        foreach (var facet in Facets)
        {
            // Only the facets the cap can apply to, asked through the same predicate every other
            // call site here asks, so the stored set holds no key a lifted cap could never honour.
            if (FacetLimits.IsLong(facet.Options.Count))
            {
                _expandedFacets[facet.Key] = facet.Options.Count;
            }
        }
    }

    /// <summary>Whether the reader has pressed this facet's own "Vis N til", over these values.</summary>
    private bool IsFacetExpanded(Facet facet) =>
        FacetLimits.StillExpanded(
            _expandedFacets.TryGetValue(facet.Key, out var asked) ? asked : null,
            facet.Options.Count);

    /// <summary>Show the rest of a facet's values, or take them back behind the cap.</summary>
    /// <remarks>
    /// The standing-gesture clause every other disclosure here carries: the second click of a
    /// double-click and a shift-click both stand still, and neither is a press. (Fhi.Metadata-zel47)
    /// </remarks>
    private void ToggleFacetExpandedFromControl(Facet facet, MouseEventArgs released)
    {
        if (RowPress.WasSelectionStandingStill(released))
        {
            return;
        }

        if (IsFacetExpanded(facet))
        {
            _expandedFacets.Remove(facet.Key);
        }
        else
        {
            _expandedFacets[facet.Key] = facet.Options.Count;
        }
    }

    /// <summary>Whether the facet draws the control that reveals what the cap is holding back.</summary>
    /// <remarks>
    /// Not during the facet's own search, which draws every match; an expanded facet keeps it as the only way back.
    /// <paramref name="hidden"/> is the render's own count, never a second sum of it.
    /// </remarks>
    private bool ShowsRestControl(Facet facet, int hidden) =>
        IsSearchable(facet)
        && FacetSearchTerm(facet.Key) is null
        && (IsFacetExpanded(facet) || hidden > 0);

    /// <summary>What the control says: the remainder it would reveal, or the offer to put it back.</summary>
    private string RestControlText(Facet facet, int hidden) =>
        IsFacetExpanded(facet) ? T.ShowFewerFacetValues : T.ShowMoreFacetValues(hidden);

    /// <summary>The id joining a facet's value list to the control that reveals the rest of it.</summary>
    private string FacetOptionsId(string key) => $"munin-explorer-facet-options-{_instance}-{key}";

    /// <summary>Whether one option answers <paramref name="term"/>.</summary>
    /// <remarks>
    /// One rule, because <see cref="RemovesDrawnOptions"/> asks it of a term the field has not committed yet
    /// and two spellings of "matches" would disagree about whether focus has anywhere to go.
    /// </remarks>
    private static bool Matches(FacetOption option, string term) =>
        option.Label.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>A kilde's kategori tokens, out of the JSON array the catalogue stores as a string.</summary>
    /// <remarks>
    /// A value that is not an array is one token, not dropped: an empty Kategori is what this panel exists not to draw.
    /// A bare JSON string is unwrapped, or its quotes make a second choice; JSON null is no kategori.
    /// </remarks>
    private static IReadOnlyList<string> Categories(KildeSummary kilde)
    {
        var raw = Property(kilde, CategoryKey);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);

            switch (document.RootElement.ValueKind)
            {
                case JsonValueKind.Array:
                    return
                    [
                        .. document.RootElement.EnumerateArray()
                            .Where(element => element.ValueKind is JsonValueKind.String)
                            .Select(element => element.GetString()!.Trim())
                            .Where(token => token.Length > 0)
                    ];

                case JsonValueKind.String:
                    return One(document.RootElement.GetString());

                case JsonValueKind.Null:
                    return [];
            }
        }
        catch (JsonException)
        {
            // Not JSON at all, which is a plain value written into a field that usually holds an
            // array. It is still what the catalogue holds for this kilde.
        }

        return One(raw);
    }

    /// <summary>One of the curated properties, or null where the kilde has not got it.</summary>
    /// <remarks>
    /// Null-conditional though non-nullable: <c>NullAsEmptyCollections</c> covers only our client, which a host can
    /// swap, and this runs per kilde per render outside the fetch's try/catch, so one bad entry would sink the panel.
    /// </remarks>
    private static string? Property(KildeSummary kilde, string key) =>
        kilde.AdditionalProperties?.TryGetValue(key, out var value) == true ? value : null;

    /// <summary>A single value as a facet's list of them, and nothing at all when it is blank.</summary>
    /// <remarks>
    /// Blank is not a value: an empty option draws a checkbox with no name, and "Ikke oppgitt" would invent a catalogue
    /// value nobody can filter on anywhere else.
    /// </remarks>
    private static IReadOnlyList<string> One(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];

    /// <summary>A label cut to <see cref="FacetLabelLimit"/>, with an ellipsis to say so.</summary>
    /// <remarks>
    /// Steps back off a lone high surrogate rather than draw half a character: free text can hold a pasted emoji.
    /// </remarks>
    private static string Shorten(string label)
    {
        if (label.Length <= FacetLabelLimit)
        {
            return label;
        }

        var cut = char.IsHighSurrogate(label[FacetLabelLimit - 1]) ? FacetLabelLimit - 1 : FacetLabelLimit;

        return label[..cut].TrimEnd() + "…";
    }

    /// <summary>The panel's heading, at <see cref="FilterLevel"/>.</summary>
    /// <remarks>
    /// By hand, as the title is: Razor has no computed element name and the level follows <see cref="HeadingLevel"/>.
    /// No count: the chip row and count line are outside the fold and say it while closed. (Fhi.Metadata-l9l2n.83)
    /// </remarks>
    private RenderFragment FiltersHeading => builder =>
    {
        builder.OpenElement(0, $"h{FilterLevel}");
        builder.AddAttribute(1, "class", "headline headline-s");
        builder.AddContent(2, T.FiltersTitle);
        builder.CloseElement();
    };

    /// <summary>A facet's summary: its heading, sized as <see cref="KildeView"/>'s fact groups, and counts.</summary>
    /// <remarks>
    /// Counts sit beside the heading, drawn while folded: this panel is navigated by heading. (Fhi.Metadata-l9l2n.53)
    /// The size is <c>Options.Count</c>, never what the cap or the search leaves drawn. (Fhi.Metadata-35w0p.53)
    /// </remarks>
    private RenderFragment FacetSummary(Facet facet) => builder =>
    {
        builder.OpenElement(0, $"h{FacetLevel}");
        builder.AddAttribute(1, "class", "headline headline-xxs margin--none");
        builder.AddContent(2, facet.Heading);
        builder.CloseElement();

        // The spaces are the word breaks in the sentence the summary is announced as; the row
        // draws none between items, and without them the two counts read as one number.
        builder.AddContent(3, " ");
        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "class", "munin-explorer-filters__groupcount");
        builder.AddContent(6, T.FacetSize(facet.Options.Count));
        builder.CloseElement();

        var chosen = ChosenIn(facet.Key);

        if (chosen == 0)
        {
            return;
        }

        builder.AddContent(7, " ");
        builder.OpenElement(8, "span");
        builder.AddAttribute(9, "class", "munin-explorer-filters__chosen");
        builder.AddContent(10, T.FacetChosen(chosen));
        builder.CloseElement();
    };
}
