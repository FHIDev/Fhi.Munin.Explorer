namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The key a placement row addresses one of this package's own blocks by — the sections a detail
/// view draws itself, which hold no curated property.
/// </summary>
/// <remarks>
/// Munin's own <c>BuiltInSectionKeys</c> spelled on this side, and the spelling is the contract: a
/// key these two disagree about is a section the catalogue can place and the view never moves.
/// Norwegian slugs because they are Munin's, and the curated groups share this namespace.
/// </remarks>
internal static class SectionKeys
{
    /// <summary>The kilde page's delkilde and datasamling tree. Seeded into KildeDetalj band 3000.</summary>
    internal const string DataCollections = "datasamlinger";

    /// <summary>The kilde page's assembled fact box, which the mockup does not name.</summary>
    /// <remarks>
    /// Written down before Munin reserves it, so the section is placeable the day a seed names it
    /// and drawn at the end of the page until then — no field of it is dropped meanwhile
    /// (Fhi.Metadata-9dhcj).
    /// </remarks>
    internal const string SourceInformation = "kildeinformasjon";

    /// <summary>The kilde page's statistics box, which the mockup does not name.</summary>
    /// <inheritdoc cref="SourceInformation" path="/remarks"/>
    internal const string Statistics = "statistikk";
}
