using System.Text.RegularExpressions;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// What «Søk om tilgang» offers on a kilde page, chosen by its Tilgangsnivå (Fhi.Metadata-l9l2n.142): an application
/// through the host, else one directly to the registry, plus open data where the kilde has some.
/// </summary>
internal static partial class KildeAccess
{
    internal const string ViaHelsedata = "soknad-via-helsedata";
    internal const string RequiresApplication = "tilgang-krever-soknad";
    internal const string OpenData = "apne-data";
    internal const string StatisticsBank = "statistikkbank";

    internal const string ApplicationUrlKey = "SoknadOmTilgangUrl";
    internal const string ApplicationEmailKey = "SoknadOmTilgangEpost";
    internal const string OpenDataUrlKey = "AapneDataUrl";

    // The kilde's own site, which is where a registry without an application page says how to apply.
    private static readonly string[] WebsiteKeys = ["Hjemmeside", "landingPage"];

    internal sealed record Link(string Text, string Href, bool Primary);

    /// <summary>A catalogue word set into a sentence; Norwegian when the catalogue wrote it in Norwegian.</summary>
    internal sealed record Fill(string Text, bool Norwegian);

    /// <summary>One sentence and its links. <paramref name="Format"/> holds {0} and {1} for the catalogue's words.</summary>
    internal sealed record Offer(string Level, string Format, IReadOnlyList<Fill> Fills, IReadOnlyList<Link> Links);

    /// <summary>The sentence split at its {0} and {1}, as text and the fill that follows each piece (null after the last).</summary>
    internal static IEnumerable<(string Text, Fill? Fill)> Pieces(Offer offer)
    {
        var parts = HolePattern().Split(offer.Format);
        for (var i = 0; i < parts.Length; i += 2)
        {
            var hole = i + 1 < parts.Length ? int.Parse(parts[i + 1], System.Globalization.CultureInfo.InvariantCulture) : -1;
            yield return (parts[i], hole >= 0 && hole < offer.Fills.Count ? offer.Fills[hole] : null);
        }
    }

    internal static IReadOnlyList<Offer> Offers(KildeDetail kilde, KildeAccessLinks? links, Texts t)
    {
        var codes = AccessLevels.Codes(kilde.AdditionalProperties);
        var named = t.Named(kilde.PreferredTerm, kilde.Code);
        var name = new Fill(named.Text, named.Norwegian);
        var offers = new List<Offer>();

        if (Has(codes, ViaHelsedata))
        {
            if (ThroughHost(kilde, links, t) is { } host)
            {
                offers.Add(host);
            }
        }
        else if (Has(codes, RequiresApplication) && Direct(kilde, name, t) is { } direct)
        {
            offers.Add(direct);
        }

        if ((Has(codes, OpenData) || Has(codes, StatisticsBank))
            && (Url(Value(kilde, OpenDataUrlKey)) ?? Present(links?.OpenDataHref)) is { } open)
        {
            offers.Add(new Offer(OpenData, t.AccessOpenData, [name], [new Link(t.AccessSeeOpenData, open, Primary: false)]));
        }

        return offers;
    }

    private static Offer? ThroughHost(KildeDetail kilde, KildeAccessLinks? links, Texts t)
    {
        List<Link> found = [];
        if (Present(links?.ApplicationHref) is { } apply)
        {
            found.Add(new Link(t.AccessApply, apply, Primary: true));
        }

        if (Present(links?.PersonalDataGuideHref) is { } personal)
        {
            found.Add(new Link(t.AccessGuidePersonal, personal, Primary: false));
        }

        if (Present(links?.AnonymousDataGuideHref) is { } anonymous)
        {
            found.Add(new Link(t.AccessGuideAnonymous, anonymous, Primary: false));
        }

        if (found.Count == 0)
        {
            return null;
        }

        return Present(kilde.DataController) is { } controller
            ? new Offer(ViaHelsedata, t.AccessViaHelsedataFrom, [new Fill(controller, Norwegian: true)], found)
            : new Offer(ViaHelsedata, t.AccessViaHelsedata, [], found);
    }

    private static Offer? Direct(KildeDetail kilde, Fill name, Texts t)
    {
        if (Url(Value(kilde, ApplicationUrlKey)) is { } page)
        {
            return new Offer(RequiresApplication, t.AccessDirect, [name], [new Link(t.AccessApplyToRegistry, page, Primary: true)]);
        }

        if (Present(Value(kilde, ApplicationEmailKey)) is { } email && EmailPattern().IsMatch(email) && !email.Contains('%'))
        {
            return new Offer(RequiresApplication, t.AccessDirectByEmail, [name, new Fill(email, Norwegian: false)],
                [new Link(t.AccessSendEmail, "mailto:" + email, Primary: true)]);
        }

        return WebsiteKeys.Select(key => Website(Value(kilde, key))).FirstOrDefault(href => href is not null) is { } site
            ? new Offer(RequiresApplication, t.AccessDirectWebsite, [name], [new Link(t.AccessApplyToRegistry, site, Primary: true)])
            : null;
    }

    private static string? Website(string? raw) =>
        CatalogueMarkdown.Link(raw)?.Href ?? CatalogueMarkdown.LinkList(raw)?.FirstOrDefault();

    // A web page only: AllowedScheme also lets mailto through, which no «page» link should be.
    private static string? Url(string? raw) =>
        CatalogueMarkdown.Link(raw)?.Href is { } href && Uri.TryCreate(href, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? href : null;

    private static string? Value(KildeDetail kilde, string key) =>
        kilde.AdditionalProperties?.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool Has(IReadOnlyList<string> codes, string code) => codes.Contains(code, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\{(\d)\}")]
    private static partial Regex HolePattern();

    // Munin's own rule for its Email type; a '%' is refused besides, since a mail client decodes it in a mailto.
    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailPattern();
}
