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
    /// Title of the section the key belongs under, per language code, e.g. <c>Identifikasjon</c>.
    /// Empty when no placement files the key on the surface this payload was fetched for.
    /// </summary>
    /// <remarks>
    /// This package draws nothing for an unfiled key, and a consumer should think hard before
    /// gathering them under a catch-all heading instead: the keys that arrive without a section are
    /// the column-backed ones, which every detail page already draws in markup of its own, so a
    /// catch-all renders the same fact a second time under a second word.
    /// </remarks>
    [JsonPropertyName("groupTranslations")]
    public IReadOnlyDictionary<string, string> GroupTranslations { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Stable identifier of the section the key belongs under, e.g. <c>om-registeret</c>. Null either
    /// when the group has no key or when the API predates the field.
    /// </summary>
    /// <remarks>
    /// The key identifies a section; <see cref="GroupTranslations"/> only titles it, and a curator
    /// can rename a title without changing the key.
    /// </remarks>
    [JsonPropertyName("groupKey")] public string? GroupKey { get; init; }

    /// <summary>
    /// Ascending order of the section itself on the surface this payload was fetched for. Null
    /// either when no placement names the section there or when the API predates the field.
    /// </summary>
    /// <remarks>
    /// Munin intends to repeat it on every entry of one section, and the same section can carry a
    /// different order on another surface — the order belongs to the placement, not to the section.
    /// This package does not rely on the repetition: it takes the value off whichever entry opened
    /// the section and ignores the rest, so a ragged payload still draws one section in one place.
    /// A consumer reading null infers the section's position from its members' <see cref="SortOrder"/>
    /// instead, which is what every consumer did before this field existed and is why a section could
    /// move up the page when a previously-empty property was filled in.
    /// </remarks>
    [JsonPropertyName("groupSortOrder")] public int? GroupSortOrder { get; init; }

    /// <summary>Ascending display order within the section.</summary>
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
    /// the request's language — unless the reader's language is not the request's. Each
    /// <see cref="PropertyOption"/> carries one label, fixed at the <c>Accept-Language</c> the
    /// response was fetched under, so a caller that renders one response to readers in more than
    /// one language has nothing in <see cref="Options"/> to switch on and reads the labels from
    /// here instead. That is the case this string is for, and it is why the field is kept rather
    /// than deprecated: it is the only place both labels survive.
    /// <para>
    /// The component in <c>Fhi.Munin.Explorer.Blazor</c> is exactly that caller — it picks
    /// <c>label</c> or <c>labelEn</c> per render, from the language the reader chose — so this
    /// package's own reference implementation is on this side of the split, not the other.
    /// </para>
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
    /// The label to show. Resolved server side from editable master data and following the
    /// <c>Accept-Language</c> of the request that fetched it, so it is not a caller's to map or to
    /// cache — and, for the same reason, not a caller's to re-language: rendering one response in
    /// two languages means reading <see cref="PropertyMetadataEntry.OptionsJson"/>, where both
    /// labels are still there.
    /// </summary>
    [JsonPropertyName("displayName")] public string DisplayName { get; init; } = "";
}
