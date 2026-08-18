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
    [JsonPropertyName("variabelgrupper")] public IReadOnlyList<VariabelgruppeFacet> Variabelgrupper { get; init; } = [];

    /// <summary>
    /// Saved filter definitions from the catalogue (Munin's <c>Filter</c> entity), not the facets
    /// above. Empty in every environment probed so far, so treat the shape as unproven.
    /// </summary>
    [JsonPropertyName("filtere")] public IReadOnlyList<FilterFacet> Filters { get; init; } = [];

    [JsonPropertyName("delkilder")] public IReadOnlyList<DelkildeFacet> Delkilder { get; init; } = [];
    [JsonPropertyName("datatyper")] public IReadOnlyList<DataTypeFacet> DataTypes { get; init; } = [];

    /// <summary>Most-used helsefaglige kodeverk (V-HK) under the current selection.</summary>
    [JsonPropertyName("helsefagligKodeverk")] public IReadOnlyList<HelsefagligKodeverkFacet> HelsefagligKodeverk { get; init; } = [];

    /// <summary>Most-used administrative kodeverk (V-AK) under the current selection.</summary>
    [JsonPropertyName("administrativtKodeverk")] public IReadOnlyList<AdministrativtKodeverkFacet> AdministrativtKodeverk { get; init; } = [];

    /// <summary>Most-used instruments (questionnaires, scales) under the current selection.</summary>
    [JsonPropertyName("instrumenter")] public IReadOnlyList<InstrumentFacet> Instruments { get; init; } = [];

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
    /// Label for the value. Currently the raw enum name (<c>SentraltHelseregister</c>), not a
    /// human-friendly Norwegian phrase — a UI that wants prose supplies its own.
    /// </summary>
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

    [JsonPropertyName("kildeType")] public string KildeType { get; init; } = "";
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>A variabelgruppe facet. <see cref="ParentId"/> lets the caller rebuild the group tree.</summary>
public sealed record VariabelgruppeFacet
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("parentId")] public Guid? ParentId { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
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

/// <summary>A datatype facet.</summary>
public sealed record DataTypeFacet
{
    /// <summary>
    /// The datatype code as stored on the variable, a small integer rendered as a string
    /// (<c>"1"</c>, <c>"2"</c>, …). The endpoint returns no label for it — the meaning of each code
    /// comes from Munin's datatype kodeverk, which this API does not expose, so a UI has to carry
    /// its own mapping.
    /// </summary>
    [JsonPropertyName("value")] public string Value { get; init; } = "";

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

/// <summary>The span of data dates in the current selection. Either end is null when unknown.</summary>
public sealed record DateInterval
{
    [JsonPropertyName("min")] public DateTimeOffset? Min { get; init; }
    [JsonPropertyName("max")] public DateTimeOffset? Max { get; init; }
}
