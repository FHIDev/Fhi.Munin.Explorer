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
    /// Honoured like every other facet, and like <see cref="DatasamlingIds"/> it has no entry in
    /// <see cref="FilterOptions"/> — the tokens live on the datasamling nodes of a kilde hierarchy,
    /// so there are no cross-filtered counts to render beside them.
    /// </remarks>
    public IReadOnlyList<string> Datakategorier { get; init; } = [];

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
    /// Each selected value counts once, so two kilder and a datatype is three. The three filters
    /// that are not lists count as one each when they are set — including
    /// <see cref="IncludeHistorical"/>, which changes the result set exactly as the others do.
    /// </remarks>
    public int ActiveCount =>
        KildeIds.Count + DelkildeIds.Count + DatasamlingIds.Count + VariabelgruppeIds.Count +
        FilterIds.Count + DataTypes.Count + HelsefagligKodeverk.Count + AdministrativtKodeverk.Count +
        InstrumentIds.Count + Datakategorier.Count +
        (KildeType is null ? 0 : 1) + (HasKildekodeverk is null ? 0 : 1) +
        (DataFrom is null ? 0 : 1) + (DataTo is null ? 0 : 1) + (IncludeHistorical ? 1 : 0);

    /// <summary>Whether this narrows anything at all.</summary>
    public bool IsEmpty => ActiveCount == 0;

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

        foreach (var pair in Repeat("datakategorier", Datakategorier))
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
    /// Comma-joining them would arrive as a single malformed id, and the API would answer with an
    /// empty result rather than a complaint.
    /// </remarks>
    private static IEnumerable<(string Name, string Value)> Repeat<T>(string name, IReadOnlyList<T> values)
        where T : notnull
    {
        foreach (var value in values)
        {
            yield return (Name: name, Value: value.ToString() ?? "");
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

    /// <summary>
    /// Read a filter back from a query string, with or without a leading <c>?</c>.
    /// </summary>
    /// <remarks>
    /// Nothing throws. A parameter this record does not know, a malformed id, a date that is not a
    /// date — each is dropped and the rest of the filter is kept, because the input is a URL a
    /// person can edit and a public page has to survive that. Returns <see cref="None"/> for null,
    /// empty or entirely unrecognised input, so a host can hand it <c>Request.QueryString.Value</c>
    /// without checking first.
    /// </remarks>
    public static VariableFilter Parse(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return None;
        }

        List<Guid> kildeIds = [], delkildeIds = [], datasamlingIds = [], variabelgruppeIds = [],
                   filterIds = [], instrumentIds = [];
        List<string> dataTypes = [], helsefagligKodeverk = [], administrativtKodeverk = [], datakategorier = [];
        string? kildeType = null;
        bool? hasKildekodeverk = null;
        DateOnly? dataFrom = null, dataTo = null;
        var includeHistorical = false;

        foreach (var pair in queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = Uri.UnescapeDataString(pair[..separator]);
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (name.ToLowerInvariant())
            {
                case "kildeids": AddGuid(kildeIds, value); break;
                case "kildetype": kildeType = value; break;
                case "delkildeids": AddGuid(delkildeIds, value); break;
                case "datasamlingids": AddGuid(datasamlingIds, value); break;
                case "variabelgruppeids": AddGuid(variabelgruppeIds, value); break;
                case "filterids": AddGuid(filterIds, value); break;
                case "datatypes": dataTypes.Add(value); break;
                case "helsefagligkodeverkreferanser": helsefagligKodeverk.Add(value); break;
                case "administrativtkodeverkoids": administrativtKodeverk.Add(value); break;
                case "instrumentids": AddGuid(instrumentIds, value); break;
                case "datakategorier": datakategorier.Add(value); break;
                case "harkildekodeverk": hasKildekodeverk = Bool(value) ?? hasKildekodeverk; break;
                case "datafrom": dataFrom = Date(value) ?? dataFrom; break;
                case "datato": dataTo = Date(value) ?? dataTo; break;
                case "includehistorical": includeHistorical = Bool(value) ?? includeHistorical; break;
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
            Datakategorier = datakategorier,
            HasKildekodeverk = hasKildekodeverk,
            DataFrom = dataFrom,
            DataTo = dataTo,
            IncludeHistorical = includeHistorical
        };
    }

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
        if (Guid.TryParse(value, out var id))
        {
            ids.Add(id);
        }
    }

    private static bool? Bool(string value) => bool.TryParse(value, out var parsed) ? parsed : null;

    private static DateOnly? Date(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;

    /// <summary>ISO 8601 date, which is what the API's <c>DateTime</c> parameters accept unambiguously.</summary>
    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
