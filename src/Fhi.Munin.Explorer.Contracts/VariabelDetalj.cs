using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Full detail for one variable, as returned by <c>GET /api/explorer/variables/{id}</c>.
/// </summary>
/// <remarks>
/// A superset of <see cref="VariabelSammendrag"/>: the same identifying fields plus version
/// history, kodeverk links, statistics and the curated metadata bag. The kilde/datasamling names
/// are denormalised into the payload so a detail page needs one request, not four.
/// </remarks>
public sealed record VariabelDetalj
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable variable code, e.g. <c>V_ALS.F1.ALSFRSR1TALE</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    /// <summary>Display name, taken from the version being shown.</summary>
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    /// <summary>Description from the version being shown.</summary>
    [JsonPropertyName("beskrivelse")] public string Beskrivelse { get; init; } = "";

    [JsonPropertyName("kildeId")] public Guid KildeId { get; init; }
    [JsonPropertyName("kildeName")] public string KildeName { get; init; } = "";
    [JsonPropertyName("kildeKortNavn")] public string KildeKortNavn { get; init; } = "";
    [JsonPropertyName("kildeType")] public string KildeType { get; init; } = "";

    /// <summary>The datasamling shown as the variable's primary home; see <see cref="AlleDatasamlinger"/> for the rest.</summary>
    [JsonPropertyName("datasamlingId")] public Guid? DatasamlingId { get; init; }

    [JsonPropertyName("datasamlingName")] public string? DatasamlingName { get; init; }

    /// <summary>
    /// The primary datasamling's statistics cadence, e.g. <c>yearly</c>. Repeated here so a
    /// statistics view can render <see cref="Statistikker"/> without fetching the datasamling.
    /// </summary>
    [JsonPropertyName("datasamlingStatistikkType")] public string? DatasamlingStatistikkType { get; init; }

    /// <summary>Primary variabelgruppe; see <see cref="AlleVariabelgrupper"/> for the rest.</summary>
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
    [JsonPropertyName("versjonStatus")] public string VersjonStatus { get; init; } = "";

    /// <summary>The published version this payload was built from — matches one entry in <see cref="Versjoner"/>.</summary>
    [JsonPropertyName("versjonId")] public Guid? VersjonId { get; init; }

    /// <summary>
    /// Curated metadata for the version being shown — database reference, comment, what it
    /// replaces, and so on. Every value is a string. Labels come from <see cref="PropertyMetadata"/>.
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Version history — the same entries the timeline endpoint returns.</summary>
    [JsonPropertyName("versjoner")] public IReadOnlyList<Variabelversjon> Versjoner { get; init; } = [];

    /// <summary>Kodeverk the variable's values are drawn from.</summary>
    [JsonPropertyName("kodeverklinker")] public IReadOnlyList<Kodeverklink> Kodeverklinker { get; init; } = [];

    /// <summary>
    /// Value-frequency statistics. Empty across every variable probed on the test environment, so
    /// treat the shape as modelled-but-unverified.
    /// </summary>
    [JsonPropertyName("statistikker")] public IReadOnlyList<Statistikk> Statistikker { get; init; } = [];

    /// <summary>Every variabelgruppe the variable belongs to, not just the primary one.</summary>
    [JsonPropertyName("alleVariabelgrupper")] public IReadOnlyList<VariabelgruppeReferanse> AlleVariabelgrupper { get; init; } = [];

    /// <summary>Every datasamling the variable is pinned into, each with the period it applied there.</summary>
    [JsonPropertyName("alleDatasamlinger")] public IReadOnlyList<DatasamlingReferanse> AlleDatasamlinger { get; init; } = [];

    /// <summary>Labels, grouping and order for the keys in <see cref="AdditionalProperties"/>.</summary>
    [JsonPropertyName("propertyMetadata")] public IReadOnlyList<EgenskapMetadata> PropertyMetadata { get; init; } = [];
}

/// <summary>
/// One published version of a variable. Returned both inside <see cref="VariabelDetalj.Versjoner"/>
/// and as the whole payload of <c>GET /api/explorer/variables/{id}/timeline</c>.
/// </summary>
public sealed record Variabelversjon
{
    [JsonPropertyName("versjonId")] public Guid VersjonId { get; init; }

    /// <summary>The name as it read in this version — it can differ between versions.</summary>
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    /// <summary>The description as it read in this version.</summary>
    [JsonPropertyName("beskrivelse")] public string Beskrivelse { get; init; } = "";

    /// <summary>Start of the period this version describes the data for.</summary>
    [JsonPropertyName("gyldigFra")] public DateTimeOffset? GyldigFra { get; init; }

    /// <summary>End of that period; null on the version still in force.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? GyldigTil { get; init; }

    /// <summary><c>Active</c> or <c>Historical</c>.</summary>
    [JsonPropertyName("status")] public string Status { get; init; } = "";

    /// <summary>
    /// When the version was published in Munin — a catalogue event, unrelated to
    /// <see cref="GyldigFra"/>. Frequently null for versions imported before publishing was tracked.
    /// </summary>
    [JsonPropertyName("publishedAt")] public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Curated metadata as it stood in this version — diff two versions to see what changed.</summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();
}

/// <summary>A link from a variable to a kodeverk.</summary>
public sealed record Kodeverklink
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
    [JsonPropertyName("harKodeverdier")] public bool HarKodeverdier { get; init; }
}

/// <summary>A statistics entry for a variable, with the frequency of each code.</summary>
public sealed record Statistikk
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("code")] public string Code { get; init; } = "";
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();

    [JsonPropertyName("kodefrekvenser")] public IReadOnlyList<Kodefrekvens> Kodefrekvenser { get; init; } = [];
}

/// <summary>How often one code value occurs.</summary>
public sealed record Kodefrekvens
{
    [JsonPropertyName("code")] public string Code { get; init; } = "";
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";
    [JsonPropertyName("beskrivelse")] public string? Beskrivelse { get; init; }

    /// <summary>The counts themselves live here, keyed by period — the shape is set by the curated data.</summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();
}

/// <summary>A variabelgruppe a variable belongs to.</summary>
public sealed record VariabelgruppeReferanse
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>Parent group, so the caller can show the full path rather than a bare leaf name.</summary>
    [JsonPropertyName("parentId")] public Guid? ParentId { get; init; }
}

/// <summary>A datasamling a variable is pinned into, with the period it applied there.</summary>
public sealed record DatasamlingReferanse
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>
    /// When the variable entered this datasamling. Note this is a catalogue timestamp on the
    /// membership, not the data period — that is <see cref="VariabelDetalj.DataFrom"/>.
    /// </summary>
    [JsonPropertyName("validFrom")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>When it left; null while it is still a member.</summary>
    [JsonPropertyName("validTo")] public DateTimeOffset? ValidTo { get; init; }
}
