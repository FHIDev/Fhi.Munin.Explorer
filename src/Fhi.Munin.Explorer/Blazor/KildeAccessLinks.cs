namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The host's own pages that «Søk om tilgang» on the kilde page points to. Each is an href as the host writes it,
/// usually host-relative; one left null or empty drops its link. <c>{language}</c> in an href becomes the reader's
/// language, <c>no</c> or <c>en</c>. <see cref="Helsedata"/> is the default.
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
    /// <summary>helsedata.no's pages in the reader's language, the default unless a host configures its own.</summary>
    public static KildeAccessLinks Helsedata { get; } = new(
        ApplicationHref: "/{language}/soknader/?opprettSoknad=min-oversikt",
        PersonalDataGuideHref: "/{language}/soknadsveiledning-oversikt/personidentifiserbare-opplysninger/",
        AnonymousDataGuideHref: "/{language}/soknadsveiledning-oversikt/sok-om-anonyme-data/",
        OpenDataHref: "/{language}/apne-data/");

    /// <summary>
    /// Overrides for one language, keyed <c>no</c> or <c>en</c>. A link it leaves null is the shared one; an empty one
    /// drops that link for that language only.
    /// </summary>
    public IReadOnlyDictionary<string, KildeAccessLinks>? Languages { get; init; }

    /// <summary>The links a reader of <paramref name="language"/> (<c>no</c> or <c>en</c>) gets.</summary>
    internal KildeAccessLinks For(string language)
    {
        var own = Languages?.GetValueOrDefault(language);
        return new KildeAccessLinks(
            Fill(own?.ApplicationHref ?? ApplicationHref, language),
            Fill(own?.PersonalDataGuideHref ?? PersonalDataGuideHref, language),
            Fill(own?.AnonymousDataGuideHref ?? AnonymousDataGuideHref, language),
            Fill(own?.OpenDataHref ?? OpenDataHref, language));
    }

    private static string? Fill(string? href, string language) =>
        href?.Replace("{language}", language, StringComparison.Ordinal);
}
