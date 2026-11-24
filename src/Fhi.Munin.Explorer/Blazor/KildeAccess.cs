using System.Text.RegularExpressions;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;

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

    /// <summary>
    /// A catalogue word set into a sentence; Norwegian when the catalogue wrote it in Norwegian. With
    /// <paramref name="Href"/> it is drawn as a link.
    /// </summary>
    internal sealed record Fill(string Text, bool Norwegian, string? Href = null);

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

    /// <summary>
    /// The kilde page's buttons: «Søk om tilgang» to the application's target, then «Se åpne data» where the kilde has
    /// open data and an address for it, its own or the host's; with open data alone, that one is the primary.
    /// </summary>
    internal static IReadOnlyList<Link> Buttons(IReadOnlyList<Offer> offers, Texts t)
    {
        List<Link> buttons = [];
        if (offers.FirstOrDefault(o => o.Level != OpenData)?.Links.FirstOrDefault(l => l.Primary) is { } apply)
        {
            buttons.Add(apply with { Text = t.AccessApply });
        }

        if (offers.FirstOrDefault(o => o.Level == OpenData)?.Links.FirstOrDefault() is { } open)
        {
            buttons.Add(open with { Primary = buttons.Count == 0 });
        }

        return buttons;
    }

    // openDataWithoutPage: the host's wording for a kilde with no open-data address; null or blank takes the package's.
    internal static IReadOnlyList<Offer> Offers(KildeDetail kilde, KildeAccessLinks? links, Texts t, string? openDataWithoutPage = null)
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

        if (Has(codes, OpenData) || Has(codes, StatisticsBank))
        {
            offers.Add((Url(Value(kilde, OpenDataUrlKey)) ?? DisplayText.Trimmed(links?.OpenDataHref)) is { } open
                ? new Offer(OpenData, t.AccessOpenData, [name], [new Link(t.AccessSeeOpenData, open, Primary: false)])
                : WithoutPage(kilde, name, t, openDataWithoutPage));
        }

        return offers;
    }

    // No link, so no button: the sentence says so, and names the kilde's own site when the catalogue has one (Fhi.Metadata-9huq2.54).
    private static Offer WithoutPage(KildeDetail kilde, Fill name, Texts t, string? hostText)
    {
        // Only {0} is the host's to use: any other hole would place or drop the website link inside its text.
        var host = DisplayText.Trimmed(hostText) is { } given ? OnlyNameHoles(given) : null;
        var lead = Lead(host) is { Length: > 0 } wanted ? wanted : Lead(t.AccessOpenDataNoLink);
        return WebsiteKeys.Select(key => Url(Website(Value(kilde, key)))).FirstOrDefault(href => href is not null) is { } site
            ? new Offer(OpenData, lead + t.AccessOpenDataHomepage, [name, new Fill(Domain(site), Norwegian: false, site)], [])
            : new Offer(OpenData, lead + ".", [name], []);
    }

    // Repeated, since taking one hole out can join the braces around it into another.
    private static string OnlyNameHoles(string text)
    {
        string before;
        do
        {
            before = text;
            text = HolePattern().Replace(text, m => m.Value == "{0}" ? m.Value : "");
        }
        while (text != before);

        return text;
    }

    private static string Lead(string? text) => (text ?? "").TrimEnd().TrimEnd('.', '!').TrimEnd();

    private static string Domain(string href)
    {
        var host = new Uri(href).Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    private static Offer? ThroughHost(KildeDetail kilde, KildeAccessLinks? links, Texts t)
    {
        List<Link> found = [];
        if (DisplayText.Trimmed(links?.ApplicationHref) is { } apply)
        {
            found.Add(new Link(t.AccessApply, apply, Primary: true));
        }

        if (DisplayText.Trimmed(links?.PersonalDataGuideHref) is { } personal)
        {
            found.Add(new Link(t.AccessGuidePersonal, personal, Primary: false));
        }

        if (DisplayText.Trimmed(links?.AnonymousDataGuideHref) is { } anonymous)
        {
            found.Add(new Link(t.AccessGuideAnonymous, anonymous, Primary: false));
        }

        if (found.Count == 0)
        {
            return null;
        }

        return DisplayText.Trimmed(kilde.DataController) is { } controller
            ? new Offer(ViaHelsedata, t.AccessViaHelsedataFrom, [new Fill(controller, Norwegian: true)], found)
            : new Offer(ViaHelsedata, t.AccessViaHelsedata, [], found);
    }

    private static Offer? Direct(KildeDetail kilde, Fill name, Texts t)
    {
        if (Url(Value(kilde, ApplicationUrlKey)) is { } page)
        {
            return new Offer(RequiresApplication, t.AccessDirect, [name], [new Link(t.AccessApplyToRegistry, page, Primary: true)]);
        }

        if (DisplayText.Trimmed(Value(kilde, ApplicationEmailKey)) is { } email && CatalogueMarkdown.Mailto(email) is { } mailto)
        {
            return new Offer(RequiresApplication, t.AccessDirectByEmail, [name, new Fill(email, Norwegian: false)],
                [new Link(t.AccessSendEmail, mailto, Primary: true)]);
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

    private static bool Has(IReadOnlyList<string> codes, string code) => codes.Contains(code, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\{([0-9])\}")]
    private static partial Regex HolePattern();
}
