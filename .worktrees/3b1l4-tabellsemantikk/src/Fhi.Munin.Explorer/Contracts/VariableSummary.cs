using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// One row in a variable search result, as returned by <c>GET /api/explorer/variables</c>.
/// </summary>
/// <remarks>
/// Hand-curated rather than generated. The set is small enough that generation would cost
/// more than it saves, and a hand-written contract is readable by the people maintaining
/// the component. A scheduled contract test round-trips live API responses through these
/// types so drift fails a build rather than a page.
/// </remarks>
public sealed record VariableSummary
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable variable code, e.g. <c>V_ALS.F1.ALSFRSR1TALE</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    /// <summary>Display name for the variable.</summary>
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    [JsonPropertyName("beskrivelse")] public string? Description { get; init; }

    [JsonPropertyName("kildeId")] public Guid? KildeId { get; init; }
    [JsonPropertyName("kildeName")] public string? KildeName { get; init; }
    [JsonPropertyName("kildeKortNavn")] public string? KildeShortName { get; init; }
    [JsonPropertyName("kildeType")] public string? KildeType { get; init; }

    /// <summary>
    /// Curated display order from the catalogue. Null when the variable has no curated order.
    /// Sort by it to reproduce the API's own default ordering.
    /// </summary>
    [JsonPropertyName("presentationOrder")] public int? PresentationOrder { get; init; }

    [JsonPropertyName("datasamlingId")] public Guid? DatasamlingId { get; init; }
    [JsonPropertyName("datasamlingName")] public string? DatasamlingName { get; init; }

    [JsonPropertyName("variabelgruppeId")] public Guid? VariabelgruppeId { get; init; }
    [JsonPropertyName("variabelgruppeName")] public string? VariabelgruppeName { get; init; }

    /// <summary>Start of the period the data covers, when known.</summary>
    [JsonPropertyName("dataFrom")] public DateTimeOffset? DataFrom { get; init; }

    /// <summary>End of the period the data covers, when known.</summary>
    [JsonPropertyName("dataTo")] public DateTimeOffset? DataTo { get; init; }

    /// <summary>
    /// Datatype code, a small integer as a string (<c>"1"</c>, <c>"2"</c>, …). Munin's datatype
    /// kodeverk is not exposed by this API, so the meaning of each code has to come from elsewhere.
    /// </summary>
    [JsonPropertyName("dataType")] public string? DataType { get; init; }

    /// <summary><c>Active</c> or <c>Historical</c>. Drafts are never exposed here.</summary>
    [JsonPropertyName("versjonStatus")] public string? VersionStatus { get; init; }

    /// <summary>The published version this row was built from — the one <see cref="VariableDetail"/> opens on.</summary>
    [JsonPropertyName("versjonId")] public Guid? VersionId { get; init; }
}
