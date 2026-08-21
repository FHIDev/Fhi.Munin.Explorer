using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Full detail for one variable, as returned by <c>GET /api/explorer/variables/{id}</c>.
/// </summary>
/// <remarks>
/// A superset of <see cref="VariableSummary"/>: the same identifying fields plus version
/// history, kodeverk links, statistics and the curated metadata bag. The kilde/datasamling names
/// are denormalised into the payload so a detail page needs one request, not four.
/// </remarks>
public sealed record VariableDetail
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable variable code, e.g. <c>V_ALS.F1.ALSFRSR1TALE</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    /// <summary>Display name, taken from the version being shown.</summary>
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    /// <summary>Description from the version being shown.</summary>
    [JsonPropertyName("beskrivelse")] public string Description { get; init; } = "";

    [JsonPropertyName("kildeId")] public Guid KildeId { get; init; }
    [JsonPropertyName("kildeName")] public string KildeName { get; init; } = "";
    [JsonPropertyName("kildeKortNavn")] public string KildeShortName { get; init; } = "";
    [JsonPropertyName("kildeType")] public string KildeType { get; init; } = "";

    /// <summary>The datasamling shown as the variable's primary home; see <see cref="AllDatasamlinger"/> for the rest.</summary>
    [JsonPropertyName("datasamlingId")] public Guid? DatasamlingId { get; init; }

    [JsonPropertyName("datasamlingName")] public string? DatasamlingName { get; init; }

    /// <summary>
    /// The primary datasamling's statistics cadence, e.g. <c>yearly</c>. Repeated here so a
    /// statistics view can render <see cref="Statistics"/> without fetching the datasamling.
    /// </summary>
    [JsonPropertyName("datasamlingStatistikkType")] public string? DatasamlingStatisticsType { get; init; }

    /// <summary>Primary variabelgruppe; see <see cref="AllVariabelgrupper"/> for the rest.</summary>
    [JsonPropertyName("variabelgruppeId")] public Guid? VariabelgruppeId { get; init; }

    [JsonPropertyName("variabelgruppeName")] public string? VariabelgruppeName { get; init; }

    /// <summary>Earliest date the variable has data for, across all its datasamlinger.</summary>
    [JsonPropertyName("dataFrom")] public DateTimeOffset? DataFrom { get; init; }

    /// <summary>Latest date with data; null means ongoing.</summary>
    [JsonPropertyName("dataTo")] public DateTimeOffset? DataTo { get; init; }

    /// <summary>
    /// Datatype code, a small integer as a string (<c>"1"</c>, <c>"2"</c>, …). Munin's datatype
    /// kodeverk is not exposed by this API, so the meaning of each code has to come from elsewhere.
    /// </summary>
    [JsonPropertyName("dataType")] public string? DataType { get; init; }

    /// <summary><c>Active</c> or <c>Historical</c>. Drafts are never exposed here.</summary>
    [JsonPropertyName("versjonStatus")] public string VersionStatus { get; init; } = "";

    /// <summary>The published version this payload was built from — matches one entry in <see cref="Versions"/>.</summary>
    [JsonPropertyName("versjonId")] public Guid? VersionId { get; init; }

    /// <summary>
    /// Curated metadata for the version being shown — database reference, comment, what it
    /// replaces, and so on. Every value is a string. Labels come from <see cref="PropertyMetadata"/>.
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Version history — the same entries the timeline endpoint returns.</summary>
    [JsonPropertyName("versjoner")] public IReadOnlyList<VariableVersion> Versions { get; init; } = [];

    /// <summary>Kodeverk the variable's values are drawn from.</summary>
    [JsonPropertyName("kodeverklinker")] public IReadOnlyList<KodeverkLink> KodeverkLinks { get; init; } = [];

    /// <summary>
    /// Value-frequency statistics. Empty across every variable probed on the test environment, so
    /// treat the shape as modelled-but-unverified.
    /// </summary>
    [JsonPropertyName("statistikker")] public IReadOnlyList<Statistic> Statistics { get; init; } = [];

    /// <summary>Every variabelgruppe the variable belongs to, not just the primary one.</summary>
    [JsonPropertyName("alleVariabelgrupper")] public IReadOnlyList<VariabelgruppeReference> AllVariabelgrupper { get; init; } = [];

    /// <summary>Every datasamling the variable is pinned into, each with the period it applied there.</summary>
    [JsonPropertyName("alleDatasamlinger")] public IReadOnlyList<DatasamlingReference> AllDatasamlinger { get; init; } = [];

    /// <summary>Labels, grouping and order for the keys in <see cref="AdditionalProperties"/>.</summary>
    [JsonPropertyName("propertyMetadata")] public IReadOnlyList<PropertyMetadataEntry> PropertyMetadata { get; init; } = [];
}

/// <summary>
/// One published version of a variable. Returned both inside <see cref="VariableDetail.Versions"/>
/// and as the whole payload of <c>GET /api/explorer/variables/{id}/timeline</c>.
/// </summary>
public sealed record VariableVersion
{
    [JsonPropertyName("versjonId")] public Guid VersionId { get; init; }

    /// <summary>The name as it read in this version — it can differ between versions.</summary>
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    /// <summary>The description as it read in this version.</summary>
    [JsonPropertyName("beskrivelse")] public string Description { get; init; } = "";

    /// <summary>Start of the period this version describes the data for.</summary>
    [JsonPropertyName("gyldigFra")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>End of that period; null on the version still in force.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? ValidTo { get; init; }

    /// <summary><c>Active</c> or <c>Historical</c>.</summary>
    [JsonPropertyName("status")] public string Status { get; init; } = "";

    /// <summary>
    /// When the version was published in Munin — a catalogue event, unrelated to
    /// <see cref="ValidFrom"/>. Frequently null for versions imported before publishing was tracked.
    /// </summary>
    [JsonPropertyName("publishedAt")] public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Curated metadata as it stood in this version — diff two versions to see what changed.</summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();
}

/// <summary>A link from a variable to a kodeverk.</summary>
public sealed record KodeverkLink
{
    /// <summary>
    /// Which kind of link this is: <c>Kildekodeverk</c> (V-KK, defined by the kilde itself),
    /// <c>AdministrativtKodeverk</c> (V-AK, a national code system) or
    /// <c>HelsefagligKodeverk</c> (V-HK, a clinical classification).
    /// </summary>
    [JsonPropertyName("kodeverkType")] public string KodeverkType { get; init; } = "";

    /// <summary>Identifier within that kodeverk type — an OID for V-AK, a catalogue reference for V-KK.</summary>
    [JsonPropertyName("kodeverkReference")] public string KodeverkReference { get; init; } = "";

    /// <summary>Resolved name of the kodeverk. Null when it could not be looked up — fall back to the reference.</summary>
    [JsonPropertyName("displayName")] public string? DisplayName { get; init; }

    /// <summary>
    /// True when the individual code values can be fetched. V-HK links never can, so a UI should
    /// not offer to expand those.
    /// </summary>
    [JsonPropertyName("harKodeverdier")] public bool HasCodeValues { get; init; }
}

/// <summary>A statistics entry for a variable, with the frequency of each code.</summary>
public sealed record Statistic
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("code")] public string Code { get; init; } = "";
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();

    [JsonPropertyName("kodefrekvenser")] public IReadOnlyList<CodeFrequency> CodeFrequencies { get; init; } = [];
}

/// <summary>How often one code value occurs.</summary>
public sealed record CodeFrequency
{
    [JsonPropertyName("code")] public string Code { get; init; } = "";
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";
    [JsonPropertyName("beskrivelse")] public string? Description { get; init; }

    /// <summary>The counts themselves live here, keyed by period — the shape is set by the curated data.</summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();
}

/// <summary>A variabelgruppe a variable belongs to.</summary>
public sealed record VariabelgruppeReference
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>Parent group, so the caller can show the full path rather than a bare leaf name.</summary>
    [JsonPropertyName("parentId")] public Guid? ParentId { get; init; }
}

/// <summary>A datasamling a variable is pinned into, with the period it applied there.</summary>
public sealed record DatasamlingReference
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>
    /// When the variable entered this datasamling. Note this is a catalogue timestamp on the
    /// membership, not the data period — that is <see cref="VariableDetail.DataFrom"/>.
    /// </summary>
    [JsonPropertyName("validFrom")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>When it left; null while it is still a member.</summary>
    [JsonPropertyName("validTo")] public DateTimeOffset? ValidTo { get; init; }
}
