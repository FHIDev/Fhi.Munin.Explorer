using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// One row in the kilde landing list, as returned by <c>GET /api/explorer/kilder</c>.
/// </summary>
/// <remarks>
/// The endpoint returns a plain array, not a <see cref="Side{T}"/> — there is no paging, and the
/// full list is around 60 kilder. Note the JSON spells the display name <c>navn</c> here, while
/// the detail endpoint calls the same value <c>preferredTerm</c>.
/// </remarks>
public sealed record KildeSammendrag
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable kilde code, e.g. <c>K_ALS</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    [JsonPropertyName("navn")] public string Navn { get; init; } = "";
    [JsonPropertyName("kortNavn")] public string? KortNavn { get; init; }

    /// <summary>
    /// e.g. <c>sentraltHelseregister</c>. Camel-cased enum name; kept as a string so a new
    /// kildetype does not break deserialisation. Spelled <c>kildetype</c> here and
    /// <c>kildeType</c> on the variable endpoints.
    /// </summary>
    [JsonPropertyName("kildetype")] public string Kildetype { get; init; } = "";

    /// <summary>False for a kilde kept for historical reference but no longer collecting data.</summary>
    [JsonPropertyName("aktiv")] public bool Aktiv { get; init; }

    /// <summary>When the kilde was registered in Munin — catalogue bookkeeping, not a data date.</summary>
    [JsonPropertyName("opprettet")] public DateTimeOffset Opprettet { get; init; }

    /// <summary>Last edit in Munin, again catalogue bookkeeping rather than a statement about the data.</summary>
    [JsonPropertyName("sistOppdatert")] public DateTimeOffset SistOppdatert { get; init; }

    [JsonPropertyName("dataansvarlig")] public string? Dataansvarlig { get; init; }
    [JsonPropertyName("databehandler")] public string? Databehandler { get; init; }

    /// <summary>e.g. <c>indirectlyIdentifiable</c>. Null when not stated.</summary>
    [JsonPropertyName("gradAvPersonidentifikasjon")] public string? GradAvPersonidentifikasjon { get; init; }

    /// <summary>Start of the kilde's period of validity (inclusive).</summary>
    [JsonPropertyName("gyldigFra")] public DateTimeOffset? GyldigFra { get; init; }

    /// <summary>End of the period of validity; null means ongoing.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? GyldigTil { get; init; }

    /// <summary>
    /// HealthDCAT-AP completeness score. Always null today — the backend does not compute one yet.
    /// The field exists so a list view has a stable thing to bind to when it starts arriving.
    /// </summary>
    [JsonPropertyName("healthDcatScore")] public int? HealthDcatScore { get; init; }

    /// <summary>
    /// True when at least one visible published variable has a description. Lets the list mark
    /// kilder that are worth opening, without counting descriptions client-side.
    /// </summary>
    [JsonPropertyName("harVariabelbeskrivelse")] public bool HarVariabelbeskrivelse { get; init; }

    /// <summary>Datasamlinger under the kilde, both direct ones and those under delkilder.</summary>
    [JsonPropertyName("datasamlingCount")] public int DatasamlingCount { get; init; }

    [JsonPropertyName("delkildeCount")] public int DelkildeCount { get; init; }

    /// <summary>Visible published variables under the kilde.</summary>
    [JsonPropertyName("totalVariables")] public int TotalVariables { get; init; }

    /// <summary>
    /// Curated free-form metadata — contact details, purpose, legal basis, HealthDCAT-AP fields
    /// and so on. Which keys appear varies per kilde and per environment, and every value is a
    /// string even when it holds JSON (<c>healthCategory</c> arrives as
    /// <c>["ehds-cat:registries-quality-of-healthcare"]</c> in a string). Labels for the keys come
    /// from the detail endpoint's <see cref="KildeDetalj.PropertyMetadata"/>.
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    public IReadOnlyDictionary<string, string?> AdditionalProperties { get; init; } =
        new Dictionary<string, string?>();
}
