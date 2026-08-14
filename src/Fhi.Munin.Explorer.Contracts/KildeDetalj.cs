using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Full detail for one kilde, as returned by <c>GET /api/explorer/kilder/{id}</c>: its own
/// metadata plus the delkilde/datasamling tree.
/// </summary>
/// <remarks>
/// Several fields exist twice — an own value and an <c>Effective…</c> value. Munin lets a
/// datasamling or delkilde inherit dataansvarlig, databehandler, identification level and validity
/// from its parent; the own value is null when nothing is set at that level, and the effective
/// value is what actually applies. Comparing the two is how a UI shows "overridden here" rather
/// than repeating the inherited value as if it were local.
/// </remarks>
public sealed record KildeDetalj
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable kilde code, e.g. <c>K_ALS</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    /// <summary>Display name. The list endpoint calls the same value <c>navn</c>.</summary>
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    [JsonPropertyName("kortNavn")] public string? KortNavn { get; init; }
    [JsonPropertyName("beskrivelse")] public string? Beskrivelse { get; init; }
    [JsonPropertyName("kildetype")] public string Kildetype { get; init; } = "";

    /// <summary>The legal basis for collecting the data, as prose.</summary>
    [JsonPropertyName("lovverk")] public string? Lovverk { get; init; }

    [JsonPropertyName("dataansvarlig")] public string? Dataansvarlig { get; init; }
    [JsonPropertyName("databehandler")] public string? Databehandler { get; init; }

    /// <summary>e.g. <c>indirectlyIdentifiable</c>. Null when not stated.</summary>
    [JsonPropertyName("gradAvPersonidentifikasjon")] public string? GradAvPersonidentifikasjon { get; init; }

    [JsonPropertyName("gyldigFra")] public DateTimeOffset? GyldigFra { get; init; }

    /// <summary>End of the period of validity; null means ongoing.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? GyldigTil { get; init; }

    [JsonPropertyName("opprettet")] public DateTimeOffset Opprettet { get; init; }
    [JsonPropertyName("sistOppdatert")] public DateTimeOffset SistOppdatert { get; init; }

    /// <summary>Curated free-form metadata; see <see cref="KildeSammendrag.AdditionalProperties"/>.</summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Labels, grouping and order for the keys in <see cref="AdditionalProperties"/>.</summary>
    [JsonPropertyName("propertyMetadata")] public IReadOnlyList<EgenskapMetadata> PropertyMetadata { get; init; } = [];

    /// <summary>Datasamlinger hanging directly off the kilde — those under a delkilde are inside <see cref="Delkilder"/>.</summary>
    [JsonPropertyName("datasamlinger")] public IReadOnlyList<KildeDatasamling> Datasamlinger { get; init; } = [];

    /// <summary>The delkilde tree. Most kilder have none; a study series such as Tromsø has one per wave.</summary>
    [JsonPropertyName("delkilder")] public IReadOnlyList<KildeDelkilde> Delkilder { get; init; } = [];

    /// <summary>Visible published variables under the whole kilde.</summary>
    [JsonPropertyName("totalVariables")] public int TotalVariables { get; init; }

    /// <summary>Earliest data date across the kilde's datasamlinger — about the data, not the catalogue entry.</summary>
    [JsonPropertyName("dataFrom")] public DateTimeOffset? DataFrom { get; init; }

    /// <summary>Latest data date; null means data collection is ongoing.</summary>
    [JsonPropertyName("dataTo")] public DateTimeOffset? DataTo { get; init; }
}

/// <summary>
/// A datasamling as it appears inside <see cref="KildeDetalj"/>, with own and inherited values.
/// See the inheritance note on <see cref="KildeDetalj"/>.
/// </summary>
public sealed record KildeDatasamling
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("kortNavn")] public string? KortNavn { get; init; }
    [JsonPropertyName("beskrivelse")] public string Beskrivelse { get; init; } = "";

    /// <summary>Visible published variables pinned into this datasamling.</summary>
    [JsonPropertyName("variableCount")] public int VariableCount { get; init; }

    /// <summary>
    /// Curated display order from the catalogue. Null when nobody has ordered this one; sort by it
    /// to match the order Munin's own views use.
    /// </summary>
    [JsonPropertyName("presentationOrder")] public int? PresentationOrder { get; init; }

    /// <summary>Owning delkilde, or null when the datasamling hangs directly off the kilde.</summary>
    [JsonPropertyName("parentDelkildeId")] public Guid? ParentDelkildeId { get; init; }

    /// <summary>Own value; null means inherited — see <see cref="EffectiveDataansvarlig"/>.</summary>
    [JsonPropertyName("dataansvarlig")] public string? Dataansvarlig { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("databehandler")] public string? Databehandler { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gradAvPersonidentifikasjon")] public string? GradAvPersonidentifikasjon { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigFra")] public DateTimeOffset? GyldigFra { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? GyldigTil { get; init; }

    /// <summary>Own value if set, otherwise resolved up the delkilde chain to the kilde.</summary>
    [JsonPropertyName("effectiveDataansvarlig")] public string? EffectiveDataansvarlig { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveDatabehandler")] public string? EffectiveDatabehandler { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGradAvPersonidentifikasjon")] public string? EffectiveGradAvPersonidentifikasjon { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigFra")] public DateTimeOffset? EffectiveGyldigFra { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigTil")] public DateTimeOffset? EffectiveGyldigTil { get; init; }

    /// <summary>
    /// Always the owning kilde's kildetype — there is no per-datasamling column, so there is no
    /// own value to compare against.
    /// </summary>
    [JsonPropertyName("effectiveKildetype")] public string EffectiveKildetype { get; init; } = "";

    /// <summary>The datasamling's own curated metadata; see <see cref="KildeSammendrag.AdditionalProperties"/>.</summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();
}

/// <summary>
/// A delkilde inside <see cref="KildeDetalj"/> — a sub-source such as one wave of a study —
/// with its own values, the inherited ones, its datasamlinger and any nested delkilder.
/// </summary>
public sealed record KildeDelkilde
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable delkilde code, e.g. <c>K_TR.BIODATA</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("kortNavn")] public string? KortNavn { get; init; }
    [JsonPropertyName("beskrivelse")] public string Beskrivelse { get; init; } = "";

    /// <summary>Curated display order; null when unordered.</summary>
    [JsonPropertyName("presentationOrder")] public int? PresentationOrder { get; init; }

    /// <summary>Parent delkilde when nested, null when it hangs directly off the kilde.</summary>
    [JsonPropertyName("parentDelkildeId")] public Guid? ParentDelkildeId { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("dataansvarlig")] public string? Dataansvarlig { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("databehandler")] public string? Databehandler { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gradAvPersonidentifikasjon")] public string? GradAvPersonidentifikasjon { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigFra")] public DateTimeOffset? GyldigFra { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? GyldigTil { get; init; }

    /// <summary>Own value if set, otherwise resolved up the parent chain to the kilde.</summary>
    [JsonPropertyName("effectiveDataansvarlig")] public string? EffectiveDataansvarlig { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveDatabehandler")] public string? EffectiveDatabehandler { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGradAvPersonidentifikasjon")] public string? EffectiveGradAvPersonidentifikasjon { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigFra")] public DateTimeOffset? EffectiveGyldigFra { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigTil")] public DateTimeOffset? EffectiveGyldigTil { get; init; }

    /// <summary>Always the owning kilde's kildetype — there is no per-delkilde column.</summary>
    [JsonPropertyName("effectiveKildetype")] public string EffectiveKildetype { get; init; } = "";

    /// <summary>The delkilde's own curated metadata.</summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Datasamlinger hanging directly off this delkilde.</summary>
    [JsonPropertyName("datasamlinger")] public IReadOnlyList<KildeDatasamling> Datasamlinger { get; init; } = [];

    /// <summary>Nested delkilder. The tree can be deeper than one level, so walk it recursively.</summary>
    [JsonPropertyName("children")] public IReadOnlyList<KildeDelkilde> Children { get; init; } = [];
}
