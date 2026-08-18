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
    /// "labelEn":"Central health registry"}]</c>. Usually the literal <c>"[]"</c>. Deliberately left
    /// as the raw string: the option shape is not part of this contract, and a caller that needs it
    /// parses it itself.
    /// </summary>
    [JsonPropertyName("optionsJson")] public string? OptionsJson { get; init; }
}
