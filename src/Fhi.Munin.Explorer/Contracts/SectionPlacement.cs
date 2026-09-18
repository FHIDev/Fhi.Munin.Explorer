using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// One section of a detail page, in the order the catalogue draws it: both the sections built out
/// of curated properties and the ones a view draws itself.
/// </summary>
/// <remarks>
/// A page's section order is one list rather than a run of property groups followed by whatever the
/// view hard-coded after them. Munin's mockups interleave the two — the kilde page puts its
/// datasamlinger between Innhold and Tilgang og ansvar — which no view ordering the two kinds
/// separately can reproduce.
/// <para>
/// A built-in section carries no <see cref="PropertyMetadataEntry"/>, so nothing else could order
/// it against the property sections; that is why the API sends this collection at all rather than
/// leaving a consumer to infer the order from <see cref="PropertyMetadataEntry.GroupSortOrder"/>.
/// </para>
/// <para>
/// Empty against an API that predates the field, which is a detail page drawing exactly as it did
/// before placements: the property groups together, then the view's own blocks in the order the
/// view declares them.
/// </para>
/// </remarks>
public sealed record SectionPlacement
{
    /// <summary>
    /// Stable identity, e.g. <c>om-registeret</c> or <c>datasamlinger</c>.
    /// </summary>
    /// <remarks>
    /// Matches <see cref="PropertyMetadataEntry.GroupKey"/> for a property section. Both kinds
    /// share one namespace, so a key names a property section or a built-in one and never both.
    /// </remarks>
    [JsonPropertyName("groupKey")] public string Key { get; init; } = "";

    /// <summary>The heading per language code (<c>no</c>, <c>en</c>); empty where none is curated.</summary>
    /// <remarks>
    /// Carried for the contract's sake, and read by no view in this package: a property section is
    /// titled from <see cref="PropertyMetadataEntry.GroupTranslations"/>, which the same curation
    /// feeds, and a built-in section keeps the word the view has for it — one that can follow the
    /// payload, as the kilde page's does when the source has delkilder. A placement decides where a
    /// section goes and never what it is called, so renaming one here renames nothing a reader sees
    /// unless the property metadata beside it is renamed too.
    /// </remarks>
    [JsonPropertyName("groupTranslations")]
    public IReadOnlyDictionary<string, string> Translations { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// The section's own ascending order on this surface. Null where no placement names it.
    /// </summary>
    /// <remarks>
    /// Null is not a position of zero: the API has already sorted this collection, so the order to
    /// draw in is the order it arrived in whether or not a placement named each entry. The views
    /// here therefore never re-sort on this value — an unplaced entry has no band to sort by, and
    /// sorting the rest around it would move it somewhere the API did not put it. It is here for a
    /// host laying the sections out itself, which can then read the band a curator chose.
    /// </remarks>
    [JsonPropertyName("groupSortOrder")] public int? SortOrder { get; init; }

    /// <summary>True for a section the page draws itself, which holds no curated property.</summary>
    [JsonPropertyName("isBuiltIn")] public bool IsBuiltIn { get; init; }
}
