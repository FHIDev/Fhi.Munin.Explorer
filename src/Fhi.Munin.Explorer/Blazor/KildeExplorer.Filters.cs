using System.Text.Json;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

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
/// </remarks>
public sealed partial class KildeExplorer
{
    /// <summary>
    /// How much of a facet label is drawn before it is cut short.
    /// </summary>
    /// <remarks>
    /// A hard cap in C# rather than an ellipsis in CSS, because the package ships no CSS and a host
    /// that supplies no rule of its own would otherwise get the whole 200-character databehandler
    /// sentence laid out inside a 384-pixel column. The full value is on the <c>title</c>, so
    /// nothing is lost — and the cut is only ever cosmetic: the value the checkbox filters on is the
    /// one the catalogue sent, whole.
    /// </remarks>
    private const int FacetLabelLimit = 60;

    /// <summary>The key of the additional property holding a kilde's EHDS categories.</summary>
    private const string CategoryKey = "healthCategory";

    /// <summary>The key of the additional property holding a kilde's access-rights token.</summary>
    private const string AccessRightsKey = "accessRights";

    /// <summary>What a facet is, before it has been counted: where its values come from and what they are called.</summary>
    /// <param name="Key">Stable across renders, so a selection belongs to one facet and to no other.</param>
    /// <param name="Heading">The facet's own heading, in the reader's language.</param>
    /// <param name="Values">
    /// A kilde's values for this facet — none, one, or several. Several is the honest shape for
    /// kategori, which is a list per kilde; a facet that flattened it to the first would drop
    /// kilder out of a filter they belong in.
    /// </param>
    /// <param name="Label">A value as a word, where the catalogue's token is not one.</param>
    private sealed record FacetDefinition(
        string Key,
        string Heading,
        Func<KildeSummary, IReadOnlyList<string>> Values,
        Func<string, string> Label);

    /// <summary>A facet as the panel draws it: a heading and the choices under it.</summary>
    private sealed record Facet(string Key, string Heading, IReadOnlyList<FacetOption> Options);

    /// <summary>
    /// One choice inside a facet.
    /// </summary>
    /// <param name="Value">
    /// The catalogue's own value, matched whole. Not the label: two access-rights tokens with
    /// different prefixes are two values however alike their words are.
    /// </param>
    /// <param name="Label">The value as a reader should see it, at whatever length the catalogue wrote it.</param>
    /// <param name="Count">
    /// How many kilder in the whole list carry this value — see the remarks on the class for why
    /// that is not the same number Runa would show.
    /// </param>
    /// <param name="Language">
    /// The catalogue's own language where <paramref name="Label"/> holds the catalogue's words, and
    /// nothing at all where it holds this package's — see <see cref="Option"/> for which is which.
    /// </param>
    private sealed record FacetOption(string Value, string Label, int Count, string? Language)
    {
        /// <summary>The choice's visible text: its label, cut to length, and its count.</summary>
        /// <remarks>
        /// The count is inside the label's own text rather than beside it in an element of its own,
        /// for the reason the variable explorer's facet buttons put it there: it becomes part of the
        /// checkbox's accessible name, where a sibling element would be announced as a stray number
        /// or skipped altogether.
        /// </remarks>
        public string Text => $"{Shorten(Label)} ({Count})";

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
    /// Ordinal, like everything else this component compares: a case-insensitive set would fold two
    /// catalogue values into one and tick both boxes from one click.
    /// </remarks>
    private readonly Dictionary<string, HashSet<string>> _chosen = new(StringComparer.Ordinal);

    /// <summary>Whether the panel is unfolded. See the markup for why the reader can still see it while this is false.</summary>
    private bool _filtersOpen;

    /// <summary>The four definitions, built once — see <see cref="Definitions"/> for why they are held at all.</summary>
    private IReadOnlyList<FacetDefinition>? _definitions;

    /// <summary>Which reader <see cref="_definitions"/> was built for, so a host that changes language gets new headings.</summary>
    private string? _definitionsReader;

    private string FacetsId => $"munin-explorer-filters-{_instance}";

    private string FacetHeadingId(string key) => $"munin-explorer-facet-{key}-{_instance}";

    /// <summary>
    /// The panel heading's level: one below the component's own title, so the outline stays
    /// unbroken however deep the host mounted us.
    /// </summary>
    /// <remarks>
    /// The same level an open kilde's name gets, and not a clash: the panel is drawn in the list
    /// branch and the kilde in the drilldown, so the two are never on screen together.
    /// </remarks>
    private int FilterLevel => Math.Clamp(TitleLevel + 1, 1, 6);

    /// <summary>A facet heading's level: one below the panel's own heading.</summary>
    private int FacetLevel => Math.Clamp(FilterLevel + 1, 1, 6);

    /// <summary>
    /// The four facets, in Kelda's order, and where each of them reads its values.
    /// </summary>
    /// <remarks>
    /// Kildetype and databehandler are columns of the list itself. The other two are curated
    /// properties, which the list endpoint carries as a bag of strings with no vocabulary beside it
    /// — the labels live on the detail endpoint, one request per kilde — so the words come from
    /// this package's own translations where it has them and from the catalogue's token where it
    /// does not.
    /// <para>
    /// Held rather than rebuilt per read, which is the one place in this file where that is worth
    /// doing: <see cref="Facets"/> reads it once per render, but <see cref="MatchesFacets"/> reads
    /// it once per <em>kilde</em>, so a collection expression here would allocate an array and its
    /// eight closures for every row the filter is asked about. Nothing in it goes stale between
    /// renders — the label functions read <c>T</c> when they are called, not when they are built —
    /// except the four headings, which are the strings they were when the list was made. So the
    /// held list belongs to a reader, and a host that changes <see cref="Language"/> gets a new one.
    /// </para>
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
                new("kildetype", T.ColumnKildetype,
                    kilde => One(kilde.Kildetype), value => T.KildeTypeLabel(value, value)),
                new("kategori", T.FacetCategory, Categories, value => T.HealthCategoryLabel(value)),
                new("tilgangsniva", T.FacetAccessLevel,
                    kilde => One(Property(kilde, AccessRightsKey)), value => T.AccessRightsLabel(value)),

                // No label function of its own: databehandler is free text the catalogue stores as
                // somebody typed it, so there is nothing to look it up in and the value is the word.
                new("databehandler", T.FieldDataProcessor, kilde => One(kilde.DataProcessor), value => value)
            ];
        }
    }

    /// <summary>The facets worth drawing, counted over the whole list.</summary>
    /// <remarks>
    /// Built per render rather than cached, for the reason the variable explorer's are: a cached
    /// facet and the rows beside it can describe two different moments. It is four passes over some
    /// tens of records.
    /// </remarks>
    private IReadOnlyList<Facet> Facets =>
        [.. Definitions.Select(Build).Where(facet => facet.Options.Count > 0)];

    /// <summary>How many values are ticked across every facet — what the folded panel is hiding.</summary>
    private int ChosenCount => _chosen.Values.Sum(values => values.Count);

    /// <summary>One facet, counted.</summary>
    /// <remarks>
    /// Distinct per kilde, so a kilde that lists one kategori twice is one kilde in that count
    /// rather than two. Ordered by the label, in the catalogue's own collation — these are
    /// Norwegian names whoever is reading, so æ, ø and å belong at the end of the alphabet — and
    /// then by the value, because two values sharing a label would otherwise be left in whatever
    /// order the dictionary happened to enumerate.
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

    /// <summary>One choice, with the language of the words it is drawn in.</summary>
    /// <remarks>
    /// A label the catalogue wrote is marked as the catalogue's language, exactly as the same
    /// string is in the table's cells: a Norwegian organisation's name inside an English page is
    /// read out with English phonetics otherwise, which is WCAG 3.1.2. A label this package
    /// supplied is not marked, because it is already in the reader's language and a <c>lang</c> it
    /// is not in is the same failure the other way round.
    /// <para>
    /// Which of the two it is, is not a property of the facet: three of the four look their values
    /// up in a vocabulary, and every one of those falls back to the catalogue's own token where the
    /// vocabulary has no word for it. So the question is asked of the answer — a label that came
    /// back as the value itself is the catalogue's text, whether because the facet has no
    /// vocabulary at all (databehandler, which is free text) or because the lookup missed
    /// (a kildetype, CURIE or category added after this package's copy was taken).
    /// </para>
    /// </remarks>
    private FacetOption Option(FacetDefinition definition, string value, int count)
    {
        var label = definition.Label(value);
        var language = string.Equals(label, value, StringComparison.Ordinal) ? CatalogueLang(value) : null;

        return new FacetOption(value, label, count, language);
    }

    /// <summary>Whether <paramref name="kilde"/> survives every facet the reader has chosen in.</summary>
    /// <remarks>
    /// OR within a facet, AND across facets. Two values ticked in one facet leave the kilder
    /// matching <em>either</em>, which is the whole reason the values are a set rather than a
    /// single choice; a kilde has to satisfy every facet that has a choice in it, which is what
    /// makes two facets narrow rather than widen.
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

    private void ToggleFilters() => _filtersOpen = !_filtersOpen;

    /// <summary>A kilde's kategori tokens, out of the JSON array the catalogue stores them in.</summary>
    /// <remarks>
    /// Every value in the additional-properties bag is a string, including the ones that hold JSON:
    /// kategori arrives as <c>["ehds-cat:registries-quality-of-healthcare"]</c> — see
    /// <see cref="KildeSummary.AdditionalProperties"/>. So the array is read as one, and a value
    /// that is not an array is taken as a single token rather than dropped: showing what the
    /// catalogue holds beats showing nothing, and a facet quietly missing its values is exactly the
    /// empty Kategori this component was written not to draw.
    /// <para>
    /// "Taken as a single token" means the value the catalogue holds, not the text it wrote it in,
    /// and the two differ for exactly one shape. A bare <c>"ehds-cat:biobanks"</c> — one JSON
    /// string where the array case proves the field usually holds several — parses without
    /// throwing, so the raw text would come back through with its quote marks still on: a second
    /// checkbox reading <c>"ehds-cat:biobanks"</c> beside the properly-labelled Biobanker, whose
    /// label lookup misses on the trailing quote and which filters a disjoint set of kilder. It is
    /// unwrapped instead, so one category is one choice however the catalogue wrote it. A JSON
    /// <c>null</c> is the catalogue saying it has no kategori, and drawing a checkbox named "null"
    /// would be this package inventing a value; everything else — an object, a number — has no
    /// token inside it to prefer, so it falls through as written.
    /// </para>
    /// <para>
    /// The tokens are what the facet groups and filters on, whole; what a reader sees is
    /// <see cref="Texts.HealthCategoryLabel"/>, which is the catalogue's own word for the token
    /// where it has one and the token itself where it has not. The list endpoint sends no
    /// vocabulary, so the words are a copy of the one the detail endpoint sends — see that method
    /// for why a copy, and why the alternative was not raw CURIEs on screen: tilgangsnivå sits in
    /// the same panel with its tokens spelled out, and one panel cannot be in two minds about
    /// whether a reader of this catalogue is expected to read EHDS.
    /// </para>
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
    /// Null-conditional although <see cref="KildeSummary.AdditionalProperties"/> is declared
    /// non-nullable: its initialiser only survives a key that is <em>absent</em> from the payload,
    /// and <c>System.Text.Json</c> writes null straight over it for an explicit
    /// <c>"additionalProperties": null</c>. This runs for every kilde on every render, from the
    /// counting and from the filtering both, so one malformed entry in the list would otherwise
    /// take the whole panel down at render time — where the try/catch around the fetch is long
    /// since finished and cannot catch it.
    /// </remarks>
    private static string? Property(KildeSummary kilde, string key) =>
        kilde.AdditionalProperties?.TryGetValue(key, out var value) == true ? value : null;

    /// <summary>A single value as a facet's list of them, and nothing at all when it is blank.</summary>
    /// <remarks>
    /// Blank is not a value. A kilde with no databehandler belongs in no databehandler choice — an
    /// empty string as an option would draw a checkbox with no name, and "Ikke oppgitt" would be
    /// this package inventing a catalogue value nobody can filter on anywhere else.
    /// </remarks>
    private static IReadOnlyList<string> One(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];

    /// <summary>A label cut to <see cref="FacetLabelLimit"/>, with an ellipsis to say so.</summary>
    /// <remarks>
    /// The cut steps back off a lone high surrogate rather than splitting a pair, which would put
    /// half a character on screen. Nothing in the catalogue needs it today; a free-text field
    /// somebody pastes an emoji into is exactly the sort of thing that arrives without warning.
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

    /// <summary>
    /// The panel's heading, at <see cref="FilterLevel"/>, saying how many values are ticked.
    /// </summary>
    /// <remarks>
    /// Built by hand for the reason the component's title is: Razor has no syntax for a computed
    /// element name, and the level follows the host's choice of <see cref="HeadingLevel"/>.
    /// <para>
    /// The count is the variable explorer's own treatment of a collapsed facet, and it earns its
    /// place here for the same reason: with the panel folded away on a narrow screen, the heading
    /// is the only thing on screen that says the list is narrowed at all.
    /// </para>
    /// </remarks>
    private RenderFragment FiltersHeading => builder =>
    {
        var chosen = ChosenCount;

        builder.OpenElement(0, $"h{FilterLevel}");
        builder.AddAttribute(1, "class", "headline headline-s");
        builder.AddContent(2, chosen == 0 ? T.FiltersTitle : $"{T.FiltersTitle} ({chosen})");
        builder.CloseElement();
    };

    /// <summary>
    /// One facet's heading, at <see cref="FacetLevel"/> and carrying the id its group is named by.
    /// </summary>
    /// <remarks>
    /// <c>headline-xxs</c>, which is what <see cref="KildeView"/> gives a group of facts — so the
    /// panel's headings and the kilde's read as the same kind of thing rather than as two
    /// vocabularies in one component.
    /// </remarks>
    private RenderFragment FacetHeading(Facet facet) => builder =>
    {
        builder.OpenElement(0, $"h{FacetLevel}");
        builder.AddAttribute(1, "class", "headline headline-xxs margin--none");
        builder.AddAttribute(2, "id", FacetHeadingId(facet.Key));
        builder.AddContent(3, facet.Heading);
        builder.CloseElement();
    };
}
