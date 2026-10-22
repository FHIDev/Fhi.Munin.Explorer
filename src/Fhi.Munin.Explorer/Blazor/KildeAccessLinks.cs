namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The host's own pages that «Søk om tilgang» on the kilde page points to. Each is an href as the host writes it,
/// usually host-relative; one left null or empty drops its link. <see cref="Helsedata"/> is the default.
/// </summary>
/// <param name="ApplicationHref">Where an application through the host starts, for a kilde whose Tilgangsnivå is
/// «Søknad via helsedata.no». Without it that kilde gets no button.</param>
/// <param name="PersonalDataGuideHref">The application guide for personally identifiable data.</param>
/// <param name="AnonymousDataGuideHref">The application guide for anonymous, aggregated data.</param>
/// <param name="OpenDataHref">The host's open-data page, for a kilde with open data but no link of its own.</param>
public sealed record KildeAccessLinks(
    string? ApplicationHref = null,
    string? PersonalDataGuideHref = null,
    string? AnonymousDataGuideHref = null,
    string? OpenDataHref = null)
{
    /// <summary>
    /// helsedata.no's pages, the default unless a host configures its own. Norwegian for every reader: the site has
    /// no English version of them.
    /// </summary>
    public static KildeAccessLinks Helsedata { get; } = new(
        ApplicationHref: "/no/soknader/?opprettSoknad=min-oversikt",
        PersonalDataGuideHref: "/no/soknadsveiledning-oversikt/personidentifiserbare-opplysninger/",
        AnonymousDataGuideHref: "/no/soknadsveiledning-oversikt/sok-om-anonyme-data/",
        OpenDataHref: "/no/apne-data/");
}
