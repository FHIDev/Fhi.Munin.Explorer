using System.Globalization;
using System.Text;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Everything a variable search can be narrowed by, beyond the free-text term itself.
/// </summary>
/// <remarks>
/// <para>
/// One record rather than fifteen parameters, because the same selection has to reach two
/// endpoints: <see cref="IMuninExplorerClient.SearchVariablesAsync"/> decides which variables come
/// back, and <see cref="IMuninExplorerClient.GetFiltersAsync"/> decides what the facet counts beside
/// them describe. Passing different narrowing to the two is the one way to put a list and a set of
/// counts on screen that disagree, so they take the same argument.
/// </para>
/// <para>
/// Every list is a logical OR within its own facet and an AND between facets, which is what the
/// Explorer API does: two kilder means "either kilde", a kilde and a datatype means "both".
/// </para>
/// <para>
/// The property names are English, the domain terms are not — <c>kilde</c>, <c>delkilde</c>,
/// <c>datasamling</c>, <c>variabelgruppe</c>, <c>kildetype</c> and <c>kodeverk</c> are the names of
/// things in the Norwegian health-metadata catalogue rather than English concepts, and translating
/// them would break the link to the API's own field names. See <c>AGENTS.md</c>.
/// </para>
/// </remarks>
public sealed record VariableFilter
{
    /// <summary>No narrowing at all — every published variable the search matches.</summary>
    /// <remarks>
    /// Shared rather than allocated per use, and safe to share because the record is immutable:
    /// <c>with</c> produces a copy and never writes through to this one.
    /// </remarks>
    public static readonly VariableFilter None = new();

    /// <summary>Kilder to restrict to. Empty means every kilde.</summary>
    public IReadOnlyList<Guid> KildeIds { get; init; } = [];

    /// <summary>
    /// One kildetype, e.g. <c>sentraltHelseregister</c>. A single value rather than a list because
    /// that is what the API takes.
    /// </summary>
    public string? KildeType { get; init; }

    /// <summary>Delkilder to restrict to. Empty means every delkilde.</summary>
    public IReadOnlyList<Guid> DelkildeIds { get; init; } = [];

    /// <summary>
    /// Datasamlinger to restrict to.
    /// </summary>
    /// <remarks>
    /// Carried because the API filters on it, and honoured whenever it is set — but nothing in
    /// <see cref="FilterOptions"/> offers datasamlinger as a facet, so a UI built from the facets
    /// alone has no counted values to draw and no way for a reader to pick one. Reaching the
    /// datasamling level needs <see cref="IMuninExplorerClient.GetKildeHierarchyAsync"/>, one call
    /// per kilde, whose node counts are the kilde's own totals and not counts cross-filtered
    /// against the current selection.
    /// </remarks>
    public IReadOnlyList<Guid> DatasamlingIds { get; init; } = [];

    /// <summary>Variabelgrupper to restrict to. Empty means every group.</summary>
    public IReadOnlyList<Guid> VariabelgruppeIds { get; init; } = [];

    /// <summary>Saved catalogue filters to restrict to — see <see cref="FilterOptions.Filters"/>.</summary>
    public IReadOnlyList<Guid> FilterIds { get; init; } = [];

    /// <summary>Datatype codes, as <see cref="DataTypeFacet.Value"/> reports them.</summary>
    public IReadOnlyList<string> DataTypes { get; init; } = [];

    /// <summary>Helsefaglige kodeverk, by short name — <see cref="HelsefagligKodeverkFacet.ShortName"/>.</summary>
    public IReadOnlyList<string> HelsefagligKodeverk { get; init; } = [];

    /// <summary>Administrative kodeverk, by OID — <see cref="AdministrativtKodeverkFacet.Oid"/>.</summary>
    public IReadOnlyList<string> AdministrativtKodeverk { get; init; } = [];

    /// <summary>Instruments to restrict to. Empty means every instrument.</summary>
    public IReadOnlyList<Guid> InstrumentIds { get; init; } = [];

    /// <summary>
    /// EHDS datakategori tokens, e.g. <c>ehds-cat:biobanks</c>, matched whole.
    /// </summary>
    /// <remarks>
    /// The same tokens <see cref="HierarchyDatasamling.Categories"/> carries, and named the same
    /// way: <c>datakategori</c> has an honest English equivalent this package already uses, and two
    /// names for one concept on a published surface is a consumer's problem forever. The wire name
    /// is unaffected — <see cref="ToQuery"/> spells it <c>datakategorier</c>, which is what the API
    /// binds.
    /// <para>
    /// Honoured like every other facet, and like <see cref="DatasamlingIds"/> it has no entry in
    /// <see cref="FilterOptions"/> — the tokens live on the datasamling nodes of a kilde hierarchy,
    /// so there are no cross-filtered counts to render beside them.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>
    /// <c>true</c> keeps only variables that have a kildekodeverk (V-KK) link, <c>false</c> only
    /// those that do not, and null does not filter on it at all.
    /// </summary>
    public bool? HasKildekodeverk { get; init; }

    /// <summary>Only variables with data from this date onwards.</summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> rather than a <see cref="DateTimeOffset"/>: the API takes a date,
    /// and a value with a time and an offset in it would be two ways of writing the same filter that
    /// a shared link could disagree about. <see cref="FilterOptions.DateRange"/> reports the bounds
    /// as instants; take their <see cref="DateTimeOffset.Date"/> to seed a picker from them.
    /// </remarks>
    public DateOnly? DataFrom { get; init; }

    /// <summary>Only variables with data up to this date. See <see cref="DataFrom"/>.</summary>
    public DateOnly? DataTo { get; init; }

    /// <summary>
    /// Include variables whose every published version has expired. Off by default, which is also
    /// the API's default.
    /// </summary>
    public bool IncludeHistorical { get; init; }

    /// <summary>How many separate choices are active, which is what a UI counts in "3 filtre".</summary>
    /// <remarks>
    /// <para>
    /// Each selected value counts once, so two kilder and a datatype is three. The filters that are
    /// not lists count as one each when they are set — including <see cref="IncludeHistorical"/>,
    /// which changes the result set exactly as the others do.
    /// </para>
    /// <para>
    /// Counted off <see cref="ToQuery"/> rather than off the properties, so "how many filters" and
    /// "which filters go on the wire" cannot describe different filters. Counting the properties
    /// instead makes a value that is set but not sent — <c>KildeType = ""</c>, a blank entry in a
    /// list — count here while <see cref="Equals(VariableFilter)"/> calls the filter equal to
    /// <see cref="None"/>: a UI would then say "Filtre (1)" over a live clear button whose press
    /// asks for the filter already in force and does nothing, with no other control able to reach
    /// that state either.
    /// </para>
    /// </remarks>
    public int ActiveCount => ToQuery().Count();

    /// <summary>Whether this narrows anything at all.</summary>
    public bool IsEmpty => ActiveCount == 0;

    /// <summary>The query keys this filter reads and writes, so a host can tell them from its own.</summary>
    /// <remarks>
    /// The facet half of what an explorer link carries; <see cref="ExplorerUrlState.QueryKeys"/>
    /// composes it with the rest. Case-insensitive, because <see cref="Parse"/> is — an ordinal
    /// membership test would miss <c>?KildeIds=</c>, keep it as one of the host's own, and end up
    /// with the parameter in the URL twice.
    /// </remarks>
    public static IReadOnlySet<string> QueryKeys { get; } = new HashSet<string>(
    [
        "kildeIds", "kildeType", "delkildeIds", "datasamlingIds", "variabelgruppeIds", "filterIds",
        "datatypes", "helsefagligKodeverkReferanser", "administrativtKodeverkOids", "instrumentIds",
        "datakategorier", "harKildekodeverk", "dataFrom", "dataTo", "includeHistorical"
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The filter as query-string parameters, using the Explorer API's own names.
    /// </summary>
    /// <remarks>
    /// Values are unescaped — <see cref="ToQueryString"/> is what escapes them. A list repeats its
    /// name once per value, which is how the API binds one, and a parameter with nothing to say is
    /// left out entirely rather than sent empty.
    /// </remarks>
    public IEnumerable<(string Name, string Value)> ToQuery()
    {
        foreach (var pair in Repeat("kildeIds", KildeIds))
        {
            yield return pair;
        }

        if (!string.IsNullOrWhiteSpace(KildeType))
        {
            yield return ("kildeType", KildeType);
        }

        foreach (var pair in Repeat("delkildeIds", DelkildeIds))
        {
            yield return pair;
        }

        foreach (var pair in Repeat("datasamlingIds", DatasamlingIds))
        {
            yield return pair;
        }

        foreach (var pair in Repeat("variabelgruppeIds", VariabelgruppeIds))
        {
            yield return pair;
        }

        foreach (var pair in Repeat("filterIds", FilterIds))
        {
            yield return pair;
        }

        foreach (var pair in Repeat("datatypes", DataTypes))
        {
            yield return pair;
        }

        foreach (var pair in Repeat("helsefagligKodeverkReferanser", HelsefagligKodeverk))
        {
            yield return pair;
        }

        foreach (var pair in Repeat("administrativtKodeverkOids", AdministrativtKodeverk))
        {
            yield return pair;
        }

        foreach (var pair in Repeat("instrumentIds", InstrumentIds))
        {
            yield return pair;
        }

        foreach (var pair in Repeat("datakategorier", Categories))
        {
            yield return pair;
        }

        if (HasKildekodeverk is { } hasKildekodeverk)
        {
            yield return ("harKildekodeverk", hasKildekodeverk ? "true" : "false");
        }

        if (DataFrom is { } from)
        {
            yield return ("dataFrom", Date(from));
        }

        if (DataTo is { } to)
        {
            yield return ("dataTo", Date(to));
        }

        // Only when true. The API defaults to false, and a shorter URL caches better on a public
        // page — the same reasoning the client applies to the sort parameters.
        if (IncludeHistorical)
        {
            yield return ("includeHistorical", "true");
        }
    }

    /// <summary>One parameter per value, which is how the API binds a list.</summary>
    /// <remarks>
    /// <para>
    /// Comma-joining them would arrive as a single malformed id, and the API would answer with an
    /// empty result rather than a complaint.
    /// </para>
    /// <para>
    /// A blank value is left out, for the same reason a blank <see cref="KildeType"/> is: a bare
    /// <c>datatypes=</c> narrows nothing at the API and is dropped by <see cref="Parse"/>, so
    /// sending it would make a shared link come back as a different filter than it went out as —
    /// and, through <see cref="ActiveCount"/>, be counted as a filter nothing could clear. A
    /// <see cref="Guid"/> can never write one, so the check only ever fires on the free-form facets.
    /// </para>
    /// </remarks>
    private static IEnumerable<(string Name, string Value)> Repeat<TValue>(string name, IReadOnlyList<TValue> values)
        where TValue : notnull
    {
        foreach (var value in values)
        {
            var text = value.ToString();

            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return (Name: name, Value: text);
            }
        }
    }

    /// <summary>
    /// The filter as an escaped query string with no leading <c>?</c>, empty when nothing is set.
    /// </summary>
    /// <remarks>
    /// This is the form a host puts in its own URL to make a filtered search shareable, and
    /// <see cref="Parse"/> reads it back. It is also this record's canonical form for comparison —
    /// see the note on <see cref="Equals(VariableFilter)"/>.
    /// </remarks>
    public string ToQueryString()
    {
        var query = new StringBuilder();

        foreach (var (name, value) in ToQuery())
        {
            query.Append(query.Length == 0 ? "" : "&")
                 .Append(name)
                 .Append('=')
                 .Append(Uri.EscapeDataString(value));
        }

        return query.ToString();
    }

    /// <summary>How many values <see cref="Parse"/> keeps for one facet.</summary>
    /// <remarks>
    /// What <see cref="Parse"/> reads is untrusted: this renders on a public, unauthenticated page,
    /// and the remarks below invite a host to hand over whatever the request carried. The filter it
    /// produces is then held for the life of a circuit, scanned once per selected value on every
    /// render, and written back onto the outbound API URL on every fetch — so an unbounded list
    /// turns one cheap crafted link into sustained server work and a multi-megabyte upstream
    /// request. The cap is far above any selection the catalogue can offer, the API's own facets
    /// being hundreds of values rather than thousands, so it only ever truncates input that was
    /// never a selection a reader could make — which "drop what you cannot read" already allows.
    /// </remarks>
    private const int MaxValuesPerFacet = 100;

    /// <summary>How long one value may be before <see cref="Parse"/> drops it.</summary>
    /// <remarks>
    /// The bound on the free-form facets, where nothing else limits the length: a guid is 36
    /// characters and the longest value the API's facets report is a kodeverk name.
    /// </remarks>
    private const int MaxValueLength = 200;

    /// <summary>How many parameters <see cref="Parse"/> reads before ignoring the rest.</summary>
    /// <remarks>
    /// Bounds the parse itself and not only what it keeps — without it a crafted URL still costs
    /// a <see cref="Guid.TryParse(string?, out Guid)"/> per repetition before
    /// <see cref="MaxValuesPerFacet"/> discards the result. Above what every facet filled to its
    /// own cap amounts to, plus room for the host's own parameters, so a real query string is
    /// never truncated.
    /// </remarks>
    private const int MaxParameters = 2_000;

    /// <summary>
    /// Read a filter back from a query string, with or without a leading <c>?</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing throws. A parameter this record does not know, a malformed id, a date that is not a
    /// date — each is dropped and the rest of the filter is kept, because the input is a URL a
    /// person can edit and a public page has to survive that. Returns <see cref="None"/> for null,
    /// empty or entirely unrecognised input, so a host can hand it <c>Request.QueryString.Value</c>
    /// without checking first.
    /// </para>
    /// <para>
    /// Dropping extends to input that is well formed but larger than a selection can be — see
    /// <see cref="MaxValuesPerFacet"/>, <see cref="MaxValueLength"/> and
    /// <see cref="MaxParameters"/>. The caps live here rather than at the call sites because this
    /// is where untrusted input arrives, and a host that has already handed the string over has no
    /// second chance to bound it.
    /// </para>
    /// </remarks>
    public static VariableFilter Parse(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return None;
        }

        List<Guid> kildeIds = [], delkildeIds = [], datasamlingIds = [], variabelgruppeIds = [],
                   filterIds = [], instrumentIds = [];
        List<string> dataTypes = [], helsefagligKodeverk = [], administrativtKodeverk = [], categories = [];
        string? kildeType = null;
        bool? hasKildekodeverk = null;
        DateOnly? dataFrom = null, dataTo = null;
        var includeHistorical = false;

        // Keyed by the API's own parameter names, spelled exactly as ToQuery writes them a few
        // lines above: this file's one job is to keep those two lists identical, and a lowercased
        // copy of each name is a copy that can drift without anything noticing. The comparer, not
        // a fold, is what makes the match case-insensitive — as everywhere else here.
        var readers = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["kildeIds"] = value => AddGuid(kildeIds, value),
            ["kildeType"] = value => kildeType = value,
            ["delkildeIds"] = value => AddGuid(delkildeIds, value),
            ["datasamlingIds"] = value => AddGuid(datasamlingIds, value),
            ["variabelgruppeIds"] = value => AddGuid(variabelgruppeIds, value),
            ["filterIds"] = value => AddGuid(filterIds, value),
            ["datatypes"] = value => Add(dataTypes, value),
            ["helsefagligKodeverkReferanser"] = value => Add(helsefagligKodeverk, value),
            ["administrativtKodeverkOids"] = value => Add(administrativtKodeverk, value),
            ["instrumentIds"] = value => AddGuid(instrumentIds, value),
            ["datakategorier"] = value => Add(categories, value),
            ["harKildekodeverk"] = value => hasKildekodeverk = Bool(value) ?? hasKildekodeverk,
            ["dataFrom"] = value => dataFrom = Date(value) ?? dataFrom,
            ["dataTo"] = value => dataTo = Date(value) ?? dataTo,
            ["includeHistorical"] = value => includeHistorical = Bool(value) ?? includeHistorical
        };

        var read = 0;

        foreach (var pair in queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (++read > MaxParameters)
            {
                break;
            }

            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = Decode(pair[..separator]);
            var value = Decode(pair[(separator + 1)..]);

            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxValueLength)
            {
                continue;
            }

            if (readers.TryGetValue(name, out var apply))
            {
                apply(value);
            }
        }

        return new VariableFilter
        {
            KildeIds = kildeIds,
            KildeType = kildeType,
            DelkildeIds = delkildeIds,
            DatasamlingIds = datasamlingIds,
            VariabelgruppeIds = variabelgruppeIds,
            FilterIds = filterIds,
            DataTypes = dataTypes,
            HelsefagligKodeverk = helsefagligKodeverk,
            AdministrativtKodeverk = administrativtKodeverk,
            InstrumentIds = instrumentIds,
            Categories = categories,
            HasKildekodeverk = hasKildekodeverk,
            DataFrom = dataFrom,
            DataTo = dataTo,
            IncludeHistorical = includeHistorical
        };
    }

    /// <summary>One query-string token, unescaped — <c>+</c> as a space as well as <c>%XX</c>.</summary>
    /// <remarks>
    /// <see cref="Uri.UnescapeDataString(string)"/> on its own leaves <c>+</c> as itself, which is
    /// right for what <see cref="ToQueryString"/> writes — it escapes a space as <c>%20</c> — and
    /// wrong for what a host hands over. An HTML GET form, <c>WebUtility.UrlEncode</c> and
    /// <c>QueryHelpers.AddQueryString</c> all write a space as <c>+</c>, so without this
    /// <c>?helsefagligKodeverkReferanser=ICD+10</c> parses to the literal "ICD+10", goes back to the
    /// API as <c>ICD%2B10</c> and matches nothing, silently.
    /// </remarks>
    private static string Decode(string token) => Uri.UnescapeDataString(token.Replace('+', ' '));

    /// <summary>
    /// Two filters are equal when they narrow the same way, compared through
    /// <see cref="ToQueryString"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written out rather than left to the record's own equality, which would compare the eleven
    /// lists by reference: <c>filter with { KildeIds = [theSameId] }</c> would come back unequal to
    /// the filter it was copied from, so a caller asking "did anything actually change" — before a
    /// fetch, or before writing a URL — would be told yes every time.
    /// </para>
    /// <para>
    /// Order within a facet counts, because the query string preserves it. Selecting the same two
    /// kilder in the other order is a different string and therefore a different filter here; it
    /// asks the API for the same variables, so the cost of the difference is at worst one fetch
    /// nobody needed.
    /// </para>
    /// </remarks>
    public bool Equals(VariableFilter? other) =>
        other is not null &&
        (ReferenceEquals(this, other) || string.Equals(ToQueryString(), other.ToQueryString(), StringComparison.Ordinal));

    /// <inheritdoc/>
    public override int GetHashCode() => ToQueryString().GetHashCode(StringComparison.Ordinal);

    private static void AddGuid(List<Guid> ids, string value)
    {
        if (ids.Count < MaxValuesPerFacet && Guid.TryParse(value, out var id))
        {
            ids.Add(id);
        }
    }

    /// <summary>A free-form value, kept only while the facet is under its cap.</summary>
    private static void Add(List<string> values, string value)
    {
        if (values.Count < MaxValuesPerFacet)
        {
            values.Add(value);
        }
    }

    private static bool? Bool(string value) => bool.TryParse(value, out var parsed) ? parsed : null;

    private static DateOnly? Date(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;

    /// <summary>ISO 8601 date, which is what the API's <c>DateTime</c> parameters accept unambiguously.</summary>
    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
