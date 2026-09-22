using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Full detail for one datasamling, as returned by <c>GET /api/explorer/datasamling/{id}</c>.
/// </summary>
/// <remarks>
/// The same datasamling also appears nested inside <see cref="KildeDetail"/>; this endpoint is for
/// opening one directly, e.g. from a deep link, and adds the parent kilde reference needed for a
/// breadcrumb. Own vs. <c>Effective…</c> values follow the inheritance rule described on
/// <see cref="KildeDetail"/>.
/// </remarks>
public sealed record DatasamlingDetail
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable code, e.g. <c>K_ALS.INKLUSJON</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    [JsonPropertyName("kortNavn")] public string? ShortName { get; init; }

    /// <summary>Display name. Called <c>name</c> where the datasamling is nested in a kilde.</summary>
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    [JsonPropertyName("beskrivelse")] public string? Description { get; init; }
    [JsonPropertyName("opprettet")] public DateTimeOffset? Created { get; init; }
    [JsonPropertyName("sistOppdatert")] public DateTimeOffset? LastUpdated { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigFra")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? ValidTo { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("lovverk")] public string? LegalBasis { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("dataansvarlig")] public string? DataController { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("databehandler")] public string? DataProcessor { get; init; }

    /// <summary>Own value; null means inherited.</summary>
    [JsonPropertyName("gradAvPersonidentifikasjon")] public string? PersonIdentificationLevel { get; init; }

    /// <summary>
    /// How the datasamling's statistics are counted over time, e.g. <c>yearly</c>. Governs how a
    /// statistics view renders rows for continuous variables. Not inherited.
    /// </summary>
    [JsonPropertyName("statistikkType")] public string? StatisticsType { get; init; }

    /// <summary>
    /// What one row of the data represents — the unit being counted (person, episode, …). Observed
    /// as an empty string when nobody has filled it in. Not inherited.
    /// </summary>
    [JsonPropertyName("telleEnhet")] public string? CountingUnit { get; init; }

    /// <summary>How often data is collected. Not inherited.</summary>
    [JsonPropertyName("frekvens")] public string? Frequency { get; init; }

    /// <summary>Own value if set, otherwise resolved up the delkilde chain to the kilde.</summary>
    [JsonPropertyName("effectiveLovverk")] public string? EffectiveLegalBasis { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveDataansvarlig")] public string? EffectiveDataController { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveDatabehandler")] public string? EffectiveDataProcessor { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGradAvPersonidentifikasjon")] public string? EffectivePersonIdentificationLevel { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigFra")] public DateTimeOffset? EffectiveValidFrom { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveGyldigTil")] public DateTimeOffset? EffectiveValidTo { get; init; }

    /// <summary>Own value if set, otherwise inherited.</summary>
    [JsonPropertyName("effectiveInklusjonsOgEksklusjonskriterier")] public string? EffectiveInclusionAndExclusionCriteria { get; init; }

    /// <summary>
    /// Always the owning kilde's kildetype — there is no per-datasamling column. Null exactly when
    /// that kilde has none.
    /// </summary>
    [JsonPropertyName("effectiveKildetype")] public string? EffectiveKildetype { get; init; }

    /// <summary>Visible published variables pinned into this datasamling.</summary>
    [JsonPropertyName("variableCount")] public int VariableCount { get; init; }

    /// <summary>
    /// Own inclusion and exclusion criteria; null when not filled in. Use
    /// <see cref="EffectiveInclusionAndExclusionCriteria"/> for the inherited value.
    /// </summary>
    [JsonPropertyName("inklusjonsOgEksklusjonskriterier")] public string? InclusionAndExclusionCriteria { get; init; }

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

    /// <summary>Every section this page draws, property and built-in alike, in render order.</summary>
    /// <remarks>
    /// Empty against an API that predates the field — see <see cref="SectionPlacement"/> for what a
    /// view draws then. Carried rather than drawn: the datasamling page still orders its own blocks.
    /// </remarks>
    [JsonPropertyName("sections")] public IReadOnlyList<SectionPlacement> Sections { get; init; } = [];

    /// <summary>Owning delkilde, or null when the datasamling hangs directly off the kilde.</summary>
    [JsonPropertyName("parentDelkildeId")] public Guid? ParentDelkildeId { get; init; }

    /// <summary>Owning kilde — present even when the datasamling sits under a delkilde.</summary>
    [JsonPropertyName("parentKildeId")] public Guid ParentKildeId { get; init; }

    /// <summary>Owning kilde's display name, for a breadcrumb without a second request.</summary>
    [JsonPropertyName("parentKildeNavn")] public string ParentKildeName { get; init; } = "";
}
