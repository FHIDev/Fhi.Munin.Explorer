using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Full detail for one kilde, as returned by <c>GET /api/explorer/kilder/{id}</c>: its own
/// metadata plus the delkilde/datasamling tree.
/// </summary>
/// <remarks>
/// Several fields exist twice — an own value and an <c>Effective…</c> value. Munin lets a
/// datasamling or delkilde inherit data controller, data processor, identification level and
/// validity from its parent; the own value is null when nothing is set at that level, and the
/// effective value is what actually applies. Comparing the two is how a UI shows "overridden here"
/// rather than repeating the inherited value as if it were local.
/// </remarks>
public sealed record KildeDetail
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable kilde code, e.g. <c>K_ALS</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    /// <summary>Display name. The list endpoint calls the same value <c>navn</c>.</summary>
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    [JsonPropertyName("kortNavn")] public string? ShortName { get; init; }
    [JsonPropertyName("beskrivelse")] public string? Description { get; init; }
    [JsonPropertyName("kildetype")] public string Kildetype { get; init; } = "";

    /// <summary>The legal basis for collecting the data, as prose.</summary>
    [JsonPropertyName("lovverk")] public string? LegalBasis { get; init; }

    [JsonPropertyName("dataansvarlig")] public string? DataController { get; init; }
    [JsonPropertyName("databehandler")] public string? DataProcessor { get; init; }

    /// <summary>e.g. <c>indirectlyIdentifiable</c>. Null when not stated.</summary>
    [JsonPropertyName("gradAvPersonidentifikasjon")] public string? PersonIdentificationLevel { get; init; }

    [JsonPropertyName("gyldigFra")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>End of the period of validity; null means ongoing.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? ValidTo { get; init; }

    [JsonPropertyName("opprettet")] public DateTimeOffset? Created { get; init; }
    [JsonPropertyName("sistOppdatert")] public DateTimeOffset? LastUpdated { get; init; }

    /// <summary>Curated free-form metadata; see <see cref="KildeSummary.AdditionalProperties"/>.</summary>
    /// <remarks>
    /// Non-nullable, and kept so by the deserialiser rather than by the initialiser below it —
    /// see <see cref="KildeSummary.AdditionalProperties"/> for what an explicit JSON null does
    /// to that initialiser and what reads it instead.
    /// </remarks>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Labels, grouping and order for the keys in <see cref="AdditionalProperties"/>.</summary>
    [JsonPropertyName("propertyMetadata")] public IReadOnlyList<PropertyMetadataEntry> PropertyMetadata { get; init; } = [];

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
/// A datasamling as it appears inside <see cref="KildeDetail"/>, with own and inherited values.
/// See the inheritance note on <see cref="KildeDetail"/>.
/// </summary>
public sealed record KildeDatasamling
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("kortNavn")] public string? ShortName { get; init; }
    [JsonPropertyName("beskrivelse")] public string Description { get; init; } = "";

    /// <summary>Visible published variables pinned into this datasamling.</summary>
    [JsonPropertyName("variableCount")] public int VariableCount { get; init; }

    /// <summary>
    /// Curated display order from the catalogue. Null when nobody has ordered this one; sort by it
    /// to match the order Munin's own views use.
    /// </summary>
    [JsonPropertyName("presentationOrder")] public int? PresentationOrder { get; init; }

    /// <summary>Owning delkilde, or null when the datasamling hangs directly off the kilde.</summary>
    [JsonPropertyName("parentDelkildeId")] public Guid? ParentDelkildeId { get; init; }

    /// <summary>Own value; null means inherited — see <see cref="EffectiveDataController"/>.</summary>
    [JsonPropertyName("dataansvarlig")] public string? DataController { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("databehandler")] public string? DataProcessor { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gradAvPersonidentifikasjon")] public string? PersonIdentificationLevel { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigFra")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? ValidTo { get; init; }

    /// <summary>Own value if set, otherwise resolved up the delkilde chain to the kilde.</summary>
    [JsonPropertyName("effectiveDataansvarlig")] public string? EffectiveDataController { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveDatabehandler")] public string? EffectiveDataProcessor { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGradAvPersonidentifikasjon")] public string? EffectivePersonIdentificationLevel { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigFra")] public DateTimeOffset? EffectiveValidFrom { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigTil")] public DateTimeOffset? EffectiveValidTo { get; init; }

    /// <summary>
    /// Always the owning kilde's kildetype — there is no per-datasamling column, so there is no
    /// own value to compare against.
    /// </summary>
    [JsonPropertyName("effectiveKildetype")] public string EffectiveKildetype { get; init; } = "";

    /// <summary>The datasamling's own curated metadata; see <see cref="KildeSummary.AdditionalProperties"/>.</summary>
    /// <remarks>
    /// Non-nullable, and kept so by the deserialiser rather than by the initialiser below it —
    /// see <see cref="KildeSummary.AdditionalProperties"/> for what an explicit JSON null does
    /// to that initialiser and what reads it instead.
    /// </remarks>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();
}

/// <summary>
/// A delkilde inside <see cref="KildeDetail"/> — a sub-source such as one wave of a study —
/// with its own values, the inherited ones, its datasamlinger and any nested delkilder.
/// </summary>
public sealed record KildeDelkilde
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable delkilde code, e.g. <c>K_TR.BIODATA</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("kortNavn")] public string? ShortName { get; init; }
    [JsonPropertyName("beskrivelse")] public string Description { get; init; } = "";

    /// <summary>Curated display order; null when unordered.</summary>
    [JsonPropertyName("presentationOrder")] public int? PresentationOrder { get; init; }

    /// <summary>Parent delkilde when nested, null when it hangs directly off the kilde.</summary>
    [JsonPropertyName("parentDelkildeId")] public Guid? ParentDelkildeId { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("dataansvarlig")] public string? DataController { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("databehandler")] public string? DataProcessor { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gradAvPersonidentifikasjon")] public string? PersonIdentificationLevel { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigFra")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? ValidTo { get; init; }

    /// <summary>Own value if set, otherwise resolved up the parent chain to the kilde.</summary>
    [JsonPropertyName("effectiveDataansvarlig")] public string? EffectiveDataController { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveDatabehandler")] public string? EffectiveDataProcessor { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGradAvPersonidentifikasjon")] public string? EffectivePersonIdentificationLevel { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigFra")] public DateTimeOffset? EffectiveValidFrom { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigTil")] public DateTimeOffset? EffectiveValidTo { get; init; }

    /// <summary>Always the owning kilde's kildetype — there is no per-delkilde column.</summary>
    [JsonPropertyName("effectiveKildetype")] public string EffectiveKildetype { get; init; } = "";

    /// <summary>The delkilde's own curated metadata.</summary>
    /// <remarks>
    /// Non-nullable, and kept so by the deserialiser rather than by the initialiser below it —
    /// see <see cref="KildeSummary.AdditionalProperties"/> for what an explicit JSON null does
    /// to that initialiser and what reads it instead.
    /// </remarks>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Datasamlinger hanging directly off this delkilde.</summary>
    [JsonPropertyName("datasamlinger")] public IReadOnlyList<KildeDatasamling> Datasamlinger { get; init; } = [];

    /// <summary>Nested delkilder. The tree can be deeper than one level, so walk it recursively.</summary>
    [JsonPropertyName("children")] public IReadOnlyList<KildeDelkilde> Children { get; init; } = [];
}
