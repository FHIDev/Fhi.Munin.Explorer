using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Describes one key in an <c>additionalProperties</c> bag: what to call it, which group it
/// belongs under and in what order to show it.
/// </summary>
/// <remarks>
/// Munin's dynamic properties are curated per environment, so the labels cannot be hard-coded in
/// the component — the API ships the metadata alongside the values. Returned by the kilde,
/// datasamling and variabel detail endpoints, each filtered to the keys that apply to that level.
/// </remarks>
public sealed record PropertyMetadataEntry
{
    /// <summary>The key in the matching <c>additionalProperties</c> dictionary, e.g. <c>Formaal</c>.</summary>
    [JsonPropertyName("key")] public string Key { get; init; } = "";

    /// <summary>Label per language code (<c>no</c>, <c>en</c>). May be empty if nothing is curated.</summary>
    [JsonPropertyName("displayNameTranslations")]
    public IReadOnlyDictionary<string, string> DisplayNameTranslations { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Name of the section the key belongs under, per language code, e.g. <c>Identifikasjon</c>.
    /// Empty when the key is not assigned to a group — render those ungrouped rather than
    /// inventing a heading.
    /// </summary>
    [JsonPropertyName("groupTranslations")]
    public IReadOnlyDictionary<string, string> GroupTranslations { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Ascending display order within the group.</summary>
    [JsonPropertyName("sortOrder")] public int SortOrder { get; init; }

    /// <summary>
    /// How the value should be read: observed values include <c>String</c>, <c>Text</c>,
    /// <c>Number</c>, <c>Date</c>, <c>Email</c>, <c>Url</c>, <c>SingleSelect</c>,
    /// <c>MultiSelect</c>, <c>MultilingualText</c>, <c>LangTaggedList</c> and <c>Object</c>.
    /// Kept as a string so a new type added server-side does not break deserialisation.
    /// </summary>
    [JsonPropertyName("type")] public string Type { get; init; } = "";

    /// <summary>
    /// For <c>SingleSelect</c> / <c>MultiSelect</c>: the allowed options as a *JSON-encoded string*,
    /// not as JSON — e.g. <c>[{"value":"sentraltHelseregister","label":"Sentralt helseregister",
    /// "labelEn":"Central health registry"}]</c>. Usually the literal <c>"[]"</c>.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="Options"/>, which is the same list already parsed and already resolved to
    /// the request's language. This one is kept because the API still sends it, and because it is
    /// the only place the untranslated labels survive.
    /// </remarks>
    [JsonPropertyName("optionsJson")] public string? OptionsJson { get; init; }

    /// <summary>
    /// The allowed options for <c>SingleSelect</c> / <c>MultiSelect</c>, parsed by the API and
    /// resolved to the language the request asked for. Empty for every other type.
    /// </summary>
    /// <remarks>
    /// Empty against an API that predates the field, in which case a caller that needs the options
    /// falls back to parsing <see cref="OptionsJson"/> itself — which is what this package used to
    /// tell callers to do, before the API started sending the list in a shape worth having.
    /// </remarks>
    [JsonPropertyName("options")] public IReadOnlyList<PropertyOption> Options { get; init; } = [];
}

/// <summary>One allowed value of a <c>SingleSelect</c> or <c>MultiSelect</c> property.</summary>
public sealed record PropertyOption
{
    /// <summary>The value as it is stored, e.g. <c>sentraltHelseregister</c>.</summary>
    [JsonPropertyName("value")] public string Value { get; init; } = "";

    /// <summary>
    /// The label to show. Resolved server side from editable master data and following
    /// <c>Accept-Language</c>, so it is not a caller's to map or to cache.
    /// </summary>
    [JsonPropertyName("displayName")] public string DisplayName { get; init; } = "";
}
