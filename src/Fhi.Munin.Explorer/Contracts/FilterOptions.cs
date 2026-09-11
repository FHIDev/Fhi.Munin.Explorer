using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// The facets offered by <c>GET /api/explorer/filters</c> — every value the user can filter on,
/// with the number of variables behind it.
/// </summary>
/// <remarks>
/// The counts are cross-filtered: they describe the *current* selection, not the whole catalogue,
/// so the endpoint has to be re-fetched whenever a filter changes. A facet with no matches is
/// omitted from its list rather than returned with a zero count.
/// </remarks>
public sealed record FilterOptions
{
    [JsonPropertyName("kildeTyper")] public IReadOnlyList<KildetypeFacet> KildeTyper { get; init; } = [];
    [JsonPropertyName("kilder")] public IReadOnlyList<KildeFacet> Kilder { get; init; } = [];

    /// <summary>
    /// The standalone variabelgruppe facet — the flat checkbox list. Where the API answers
    /// <see cref="HierarchyVariabelgrupper"/> as well, this is a subset of it by id and a row in
    /// both carries the same values in both; against an API that predates that collection this one
    /// is populated and it is empty, so the subset relation is not something to rely on blind.
    /// </summary>
    /// <remarks>
    /// What it offers depends on the request. With a kilde, delkilde or datasamling chosen: every
    /// group the selection reaches whose <see cref="VariabelgruppeFacet.Filter"/> is not the
    /// opt-out. With none of those chosen: the groups an admin flagged
    /// <see cref="VariabelgruppeFacet.Global"/>, whose Filter is not consulted at all. A group the
    /// request already selects is added back unless it is opted out, and so are the ancestor rows
    /// needed to nest the result — which is the one way an opted-out group reaches this list.
    /// <para>
    /// So membership is not the question a caller drawing a checkbox is asking: test
    /// <see cref="VariabelgruppeFacet.IsStandaloneFacetOption"/> instead.
    /// </para>
    /// </remarks>
    [JsonPropertyName("variabelgrupper")] public IReadOnlyList<VariabelgruppeFacet> Variabelgrupper { get; init; } = [];

    /// <summary>
    /// Every variabelgruppe the kilde folder tree can draw under the current selection, whatever
    /// its <see cref="VariabelgruppeFacet.Filter"/> value — the collection a tree is built from,
    /// since the standalone facet above deliberately withholds groups the tree still has to show.
    /// </summary>
    /// <remarks>
    /// Each row carries its own <see cref="VariabelgruppeFacet.Owners"/>, so expanding a folder
    /// costs no request: this is what replaces a <c>kilder/{id}/hierarchy</c> call per expanded
    /// kilde. Counts are cross-filtered like every other facet here and never a kilde total.
    /// <para>
    /// Empty against an API that predates the collection, in which case a caller has the standalone
    /// facet and no tree.
    /// </para>
    /// </remarks>
    [JsonPropertyName("hierarkiVariabelgrupper")] public IReadOnlyList<VariabelgruppeFacet> HierarchyVariabelgrupper { get; init; } = [];

    /// <summary>
    /// Saved filter definitions from the catalogue (Munin's <c>Filter</c> entity), not the facets
    /// above. Empty in every environment probed so far, so treat the shape as unproven.
    /// </summary>
    [JsonPropertyName("filtere")] public IReadOnlyList<FilterFacet> Filters { get; init; } = [];

    [JsonPropertyName("delkilder")] public IReadOnlyList<DelkildeFacet> Delkilder { get; init; } = [];

    /// <summary>
    /// Datasamlinger under the current selection — the level below delkilde, and the one most
    /// kilder have instead of a delkilde rather than as well as one.
    /// </summary>
    /// <remarks>
    /// Empty against an API that predates the facet, in which case the source hierarchy a caller
    /// can offer stops at delkilde, as it did before.
    /// </remarks>
    [JsonPropertyName("datasamlinger")] public IReadOnlyList<DatasamlingFacet> Datasamlinger { get; init; } = [];

    [JsonPropertyName("datatyper")] public IReadOnlyList<DataTypeFacet> DataTypes { get; init; } = [];

    /// <summary>Most-used helsefaglige kodeverk (V-HK) under the current selection.</summary>
    [JsonPropertyName("helsefagligKodeverk")] public IReadOnlyList<HelsefagligKodeverkFacet> HelsefagligKodeverk { get; init; } = [];

    /// <summary>Most-used administrative kodeverk (V-AK) under the current selection.</summary>
    [JsonPropertyName("administrativtKodeverk")] public IReadOnlyList<AdministrativtKodeverkFacet> AdministrativtKodeverk { get; init; } = [];

    /// <summary>Most-used instruments (questionnaires, scales) under the current selection.</summary>
    [JsonPropertyName("instrumenter")] public IReadOnlyList<InstrumentFacet> Instruments { get; init; } = [];

    /// <summary>
    /// Datakategori facet — the same EHDS tokens a datasamling carries in
    /// <see cref="HierarchyDatasamling.Categories"/>, counted across the current selection.
    /// </summary>
    /// <remarks>
    /// Empty against an API that predates the facet, in which case there is nothing to offer and a
    /// caller shows no datakategori filter.
    /// </remarks>
    [JsonPropertyName("datakategorier")] public IReadOnlyList<DataCategoryFacet> DataCategories { get; init; } = [];

    /// <summary>
    /// Number of variables that have at least one kildekodeverk (V-KK) link. A single count rather
    /// than a facet list because the filter is a yes/no toggle, not a choice of values.
    /// </summary>
    [JsonPropertyName("kildeKodeverkCount")] public int KildeKodeverkCount { get; init; }

    /// <summary>Earliest and latest data dates in the current selection — the bounds for a date filter.</summary>
    [JsonPropertyName("dateRange")] public DateInterval? DateRange { get; init; }

    /// <summary>Total number of variables matching the current selection, before any facet is applied.</summary>
    [JsonPropertyName("totalCount")] public int TotalCount { get; init; }
}

/// <summary>A kildetype facet.</summary>
public sealed record KildetypeFacet
{
    /// <summary>The value to send back as <c>kildeType</c>, e.g. <c>sentraltHelseregister</c>.</summary>
    [JsonPropertyName("value")] public string Value { get; init; } = "";

    /// <summary>
    /// Label for the value, resolved by the API and in the request's language.
    /// </summary>
    /// <remarks>
    /// This used to be the raw enum name — <c>SentraltHelseregister</c> — so a UI wanting prose had
    /// to supply its own. It no longer does: the endpoint resolves the label and follows
    /// <c>Accept-Language</c>, so <c>sentraltHelseregister</c> arrives as
    /// <c>Sentralt helseregister</c> under <c>nb</c> and <c>Central health registry</c> under
    /// <c>en</c>. (<c>Fhi.Metadata-iv9xp</c>)
    /// <para>
    /// Key off <see cref="Value"/> and never off this text. The list is also ordered by the resolved
    /// label rather than by the value, so the facet arrives in a different order in each language —
    /// a caller mirroring the API's order elsewhere on the page inherits that.
    /// </para>
    /// </remarks>
    [JsonPropertyName("displayName")] public string DisplayName { get; init; } = "";

    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>A kilde facet.</summary>
public sealed record KildeFacet
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>Abbreviation, e.g. <c>MFR</c>. Empty string — not null — when the kilde has none.</summary>
    [JsonPropertyName("kortNavn")] public string ShortName { get; init; } = "";

    /// <summary>The kilde's kildetype; null when it has none — see <see cref="KildeSummary.Kildetype"/>.</summary>
    [JsonPropertyName("kildeType")] public string? KildeType { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>
/// A variabelgruppe on either of the two surfaces the endpoint answers with — the standalone facet
/// in <see cref="FilterOptions.Variabelgrupper"/> and the folder tree in
/// <see cref="FilterOptions.HierarchyVariabelgrupper"/>.
/// </summary>
/// <remarks>
/// One type for both, so a group chosen through either surface is the same selection and produces
/// one chip. <see cref="ParentId"/> nests the list and is never the catalogue owner: a group's
/// parent is another group, and <see cref="Owners"/> is the only member saying which kilde,
/// delkilde and datasamling it hangs under.
/// </remarks>
public sealed record VariabelgruppeFacet
{
    /// <summary>The <see cref="Filter"/> value that keeps a group out of the standalone facet.</summary>
    public const string StandaloneFacetOptOut = "2";

    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>
    /// The parent group, or null for a root. Resolvable inside whichever collection the row came
    /// from: neither withholds an ancestor of a row it returns, so the two agree on it.
    /// </summary>
    [JsonPropertyName("parentId")] public Guid? ParentId { get; init; }

    /// <summary>
    /// Variables linked directly to the group under the current selection. Cross-filtered, never a
    /// subtree roll-up, so a container reads 0 while its children carry the numbers — which keeps
    /// the number beside a checkbox equal to the rows ticking it produces.
    /// </summary>
    [JsonPropertyName("count")] public int Count { get; init; }

    /// <summary>
    /// The group's stored <c>Filter</c> property, as the catalogue holds it: <c>"2"</c> for every
    /// accepted opt-out form, null where the source file left it unset — the majority case, and
    /// "no opinion" rather than "no" — and anything else verbatim, <c>"1"</c> in practice.
    /// </summary>
    /// <remarks>
    /// Neither a bool nor an enum, deliberately: null is a third state neither could hold, and the
    /// vocabulary belongs to the source files rather than to Munin. Only <c>"2"</c> carries a rule,
    /// and only on the standalone facet — see <see cref="IsStandaloneFacetOption"/>.
    /// </remarks>
    [JsonPropertyName("filter")] public string? Filter { get; init; }

    /// <summary>
    /// Whether an admin flagged the group as globally relevant. This — not <see cref="Filter"/> —
    /// is what decides the standalone facet's membership while no kilde, delkilde or datasamling
    /// is chosen.
    /// </summary>
    /// <remarks>
    /// Not nullable, because the API's own member is not: it is denormalised into a <c>NOT NULL</c>
    /// column where every shape other than an explicit <c>true</c> — absent key, <c>false</c>, an
    /// unreadable property bag — has already collapsed to <c>false</c>.
    /// </remarks>
    [JsonPropertyName("global")] public bool Global { get; init; }

    /// <summary>Where the group hangs in the catalogue: one entry per placement a tree draws it at.</summary>
    /// <remarks>
    /// Computed over <see cref="FilterOptions.HierarchyVariabelgrupper"/> whichever collection the
    /// row is read from, and rolled up from descendants, so a container with <see cref="Count"/> 0
    /// still says where it belongs. Empty in one shape only: neither the group nor anything under
    /// it holds a variable the current selection leaves — a chosen group the other filters emptied,
    /// listed so its checkbox stays clearable, and the ancestor rows carrying it. Every row with a
    /// count has at least one placement.
    /// </remarks>
    [JsonPropertyName("owners")] public IReadOnlyList<VariabelgruppeOwner> Owners { get; init; } = [];

    /// <summary>Whether the standalone facet may offer this group as a checkbox of its own.</summary>
    /// <remarks>
    /// Membership of <see cref="FilterOptions.Variabelgrupper"/> is not that question. An opted-out
    /// group is withheld there on its own terms and comes back as the trunk an offered descendant
    /// nests under, still carrying <c>"2"</c> — so draw such a trunk as a container rather than
    /// dropping it, which would strand its children. The folder tree ignores this entirely and
    /// shows every group it is given.
    /// </remarks>
    [JsonIgnore]
    public bool IsStandaloneFacetOption => !string.Equals(Filter, StandaloneFacetOptOut, StringComparison.Ordinal);
}

/// <summary>
/// One catalogue placement of a variabelgruppe: its kilde, and as far down that kilde's hierarchy
/// as the placement reaches.
/// </summary>
/// <remarks>
/// A group holds one per datasamling its variables live in, and — rarely — under more than one
/// kilde. A placement whose kilde cannot be resolved is left out rather than returned
/// unattributed, and the variables fall back to a shallower placement, so a group with a count is
/// always drawable somewhere.
/// </remarks>
public sealed record VariabelgruppeOwner
{
    /// <summary>
    /// The owning kilde. Not nullable, because the API's own member is not — an unresolvable one
    /// costs the placement rather than the id, as the remarks above say.
    /// </summary>
    [JsonPropertyName("kildeId")] public Guid KildeId { get; init; }

    /// <summary>
    /// The delkilde the placement sits under, or null when it hangs straight off the kilde — the
    /// common case, since most kilder have no delkilde at all.
    /// </summary>
    [JsonPropertyName("delkildeId")] public Guid? DelkildeId { get; init; }

    /// <summary>
    /// The datasamling holding the variables, or null where the placement stops higher up: at a
    /// delkilde whose variables are in no reachable datasamling under it, or at the kilde where
    /// there is neither. Both are last resorts and never accompany a deeper placement of the same
    /// variable, so a group is not drawn twice.
    /// </summary>
    [JsonPropertyName("datasamlingId")] public Guid? DatasamlingId { get; init; }
}

/// <summary>A saved-filter facet. See the note on <see cref="FilterOptions.Filters"/>.</summary>
public sealed record FilterFacet
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("parentId")] public Guid? ParentId { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>
/// A delkilde facet. Carries both parents so the caller can nest it under its delkilde and group
/// it under its kilde without a second request.
/// </summary>
public sealed record DelkildeFacet
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("parentDelkildeId")] public Guid? ParentDelkildeId { get; init; }
    [JsonPropertyName("kildeId")] public Guid KildeId { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>
/// A datasamling facet. Carries both possible parents, so the caller can hang it under its
/// delkilde where it has one and under its kilde where it has none, without a second request.
/// </summary>
public sealed record DatasamlingFacet
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>The delkilde it hangs under, or null when it hangs straight off its kilde.</summary>
    [JsonPropertyName("delkildeId")] public Guid? DelkildeId { get; init; }

    [JsonPropertyName("kildeId")] public Guid KildeId { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>A datatype facet.</summary>
public sealed record DataTypeFacet
{
    /// <summary>
    /// The datatype code as stored on the variable, a small integer rendered as a string
    /// (<c>"1"</c>, <c>"2"</c>, …).
    /// </summary>
    [JsonPropertyName("value")] public string Value { get; init; } = "";

    /// <summary>
    /// The code's name, resolved by the API from the datatype property definition.
    /// </summary>
    /// <remarks>
    /// This used to be absent, and the comment here used to say a UI had to carry its own mapping.
    /// It does not: <c>Fhi.Metadata-xxi8k</c> made the endpoint resolve the name, in the request's
    /// language — send <c>Accept-Language</c> and the label follows it.
    /// <para>
    /// With one exception a caller has to handle. A variable predating the codes was stored as a
    /// word — <c>"String"</c>, <c>"tekst"</c>, <c>"Integer"</c> — and the endpoint echoes that word
    /// back here whatever the request asked for, so code <c>1</c> arrives named "String" on a
    /// Norwegian call. A caller showing this word maps the legacy spellings itself and leaves every
    /// other name alone; this package does it in <c>Texts</c>. (<c>Fhi.Metadata-l9l2n.49</c>)
    /// </para>
    /// <para>
    /// A UI should still not build its own table of names. These are editable master data, so a
    /// copy freezes a snapshot in one language and drifts the moment someone edits a definition.
    /// </para>
    /// <para>
    /// Null against an API that predates the change, in which case a caller falls back to its own
    /// word for the code, or to the code itself.
    /// </para>
    /// </remarks>
    [JsonPropertyName("displayName")] public string? DisplayName { get; init; }

    /// <summary>How many variables carry this datatype.</summary>
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>A helsefaglig kodeverk (V-HK) facet, keyed by its short name.</summary>
public sealed record HelsefagligKodeverkFacet
{
    [JsonPropertyName("kortNavn")] public string ShortName { get; init; } = "";
    [JsonPropertyName("fulltNavn")] public string FullName { get; init; } = "";
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>An administrativt kodeverk (V-AK) facet, keyed by its OID.</summary>
public sealed record AdministrativtKodeverkFacet
{
    /// <summary>OID of the code system in fhi.kodeverk, e.g. <c>3402</c> for Kommunenummer.</summary>
    [JsonPropertyName("oid")] public string Oid { get; init; } = "";

    /// <summary>Null when fhi.kodeverk could not be reached — show the OID rather than nothing.</summary>
    [JsonPropertyName("navn")] public string? Name { get; init; }

    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>An instrument facet — a questionnaire or scale a set of variables belongs to.</summary>
public sealed record InstrumentFacet
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Instrument code, e.g. <c>RAND-36</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    [JsonPropertyName("navn")] public string Name { get; init; } = "";
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>A datakategori facet.</summary>
/// <remarks>
/// The value is a token and carries no label — the same raw tokens
/// <see cref="HierarchyDatasamling.Categories"/> holds, so a caller that renders both renders them
/// the same way, and both an EHDS CURIE such as <c>ehds-cat:health-registries</c> and a bare code
/// such as <c>RPDG</c> occur there. Match whole tokens rather than on any prefix.
/// </remarks>
public sealed record DataCategoryFacet
{
    /// <summary>The value to filter on, e.g. <c>ehds-cat:population-health-surveys</c>.</summary>
    [JsonPropertyName("value")] public string Value { get; init; } = "";

    /// <summary>How many variables sit under a datasamling carrying this datakategori.</summary>
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>The span of data dates in the current selection. Either end is null when unknown.</summary>
public sealed record DateInterval
{
    [JsonPropertyName("min")] public DateTimeOffset? Min { get; init; }
    [JsonPropertyName("max")] public DateTimeOffset? Max { get; init; }
}
