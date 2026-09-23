using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Full detail for one instrument, as returned by <c>GET /api/explorer/instrument/{id}</c>.
/// </summary>
/// <remarks>
/// The questionnaire or scale a variable was collected with — reached from
/// <see cref="VariableDetail.Instruments"/>, which carries the id this endpoint is keyed by.
/// <para>
/// The variables themselves are deliberately not in the payload: an instrument can hold thousands,
/// and a caller that wants them asks the variable search with
/// <see cref="VariableFilter.InstrumentIds"/> instead. <see cref="VisibleVariableCount"/> is how
/// many that search would find today.
/// </para>
/// <para>
/// The API answers 404 — so this client answers null — for an instrument that is unknown, disabled,
/// or linked to no variable the explorer publishes.
/// </para>
/// </remarks>
public sealed record InstrumentDetail
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    /// <summary>Stable instrument code.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    /// <summary>Display name, stored in Norwegian; the English one is in <see cref="AdditionalProperties"/>.</summary>
    [JsonPropertyName("preferredTerm")] public string PreferredTerm { get; init; } = "";

    /// <summary>Description, in Norwegian; the English one is in <see cref="AdditionalProperties"/>.</summary>
    [JsonPropertyName("beskrivelse")] public string? Description { get; init; }

    [JsonPropertyName("gyldigFra")] public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>Null means the instrument is still in use.</summary>
    [JsonPropertyName("gyldigTil")] public DateTimeOffset? ValidTo { get; init; }

    /// <summary>
    /// Variables linked to this instrument that the explorer shows today. History is not counted,
    /// so it is the number a search on <see cref="VariableFilter.InstrumentIds"/> returns.
    /// </summary>
    [JsonPropertyName("visibleVariableCount")] public int VisibleVariableCount { get; init; }

    /// <summary>
    /// Curated free-form metadata; see <see cref="KildeSummary.AdditionalProperties"/>. Carries
    /// <c>NavnEngelsk</c> and <c>BeskrivelseEngelsk</c> where a curator has filled them in.
    /// </summary>
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
    /// view draws then. Carried rather than drawn: the instrument page still orders its own blocks.
    /// </remarks>
    [JsonPropertyName("sections")] public IReadOnlyList<SectionPlacement> Sections { get; init; } = [];
}
