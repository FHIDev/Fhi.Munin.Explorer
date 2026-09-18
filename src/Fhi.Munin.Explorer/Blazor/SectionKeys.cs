namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The key a placement row addresses one of this package's own blocks by — the sections a detail
/// view draws itself, which hold no curated property.
/// </summary>
/// <remarks>
/// Munin's own <c>BuiltInSectionKeys</c> spelled on this side, and the spelling is the contract: a
/// key these two disagree about is a section the catalogue can place and the view never moves.
/// <para>
/// Norwegian slugs because they are Munin's, not ours to translate — the catalogue's property
/// groups share this namespace and are named the same way.
/// </para>
/// </remarks>
internal static class SectionKeys
{
    /// <summary>The kilde page's delkilde and datasamling tree. Seeded into KildeDetalj band 3000.</summary>
    internal const string DataCollections = "datasamlinger";

    /// <summary>
    /// The kilde page's assembled fact box, which the mockup does not name.
    /// </summary>
    /// <remarks>
    /// Written down before Munin reserves it, so the section is placeable the day a seed names it
    /// and drawn at the end of the page until then — no field of it is dropped meanwhile. The seed
    /// that names both is Fhi.Metadata-9dhcj.
    /// </remarks>
    internal const string SourceInformation = "kildeinformasjon";

    /// <inheritdoc cref="SourceInformation"/>
    internal const string Statistics = "statistikk";
}
