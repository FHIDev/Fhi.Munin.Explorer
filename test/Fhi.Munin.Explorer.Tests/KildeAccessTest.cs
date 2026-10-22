using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>«Søk om tilgang» on the kilde page: what each Tilgangsnivå offers, and where the links come from (Fhi.Metadata-l9l2n.142).</summary>
public class KildeAccessTest : ExplorerTestContext
{
    private static readonly KildeAccessLinks HostLinks = KildeAccessLinks.Helsedata;

    public KildeAccessTest() => Services.AddSingleton<IMuninExplorerClient>(new HierarchyClient());

    private sealed class HierarchyClient : EmptyMuninExplorerClient
    {
        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(new() { KildeId = id });
    }

    private static KildeDetail Kilde(string levels, params (string Key, string Value)[] properties)
    {
        var bag = new Dictionary<string, string?> { [AccessLevels.Key] = levels };
        foreach (var (key, value) in properties)
        {
            bag[key] = value;
        }

        return new KildeDetail
        {
            Id = Guid.NewGuid(),
            Code = "K_TEST",
            PreferredTerm = "Testregisteret",
            DataController = "St. Olavs hospital HF",
            AdditionalProperties = bag,
        };
    }

    private static IReadOnlyList<KildeAccess.Offer> Offers(KildeDetail kilde, KildeAccessLinks? links = null) =>
        KildeAccess.Offers(kilde, links ?? HostLinks, Texts.For("no"));

    private static string Sentence(KildeAccess.Offer offer) =>
        string.Concat(KildeAccess.Pieces(offer).Select(p => p.Text + p.Fill?.Text));

    [Fact]
    public void ViaHelsedata_ThenTheFormFirst_ThenBothGuides_AndTheForvalterIsNamed()
    {
        var offer = Assert.Single(Offers(Kilde("""["soknad-via-helsedata","tilgang-krever-soknad"]""")));

        Assert.Equal(KildeAccess.ViaHelsedata, offer.Level);
        Assert.Equal(
            [("Søk om tilgang", HostLinks.ApplicationHref, true),
             ("Veileder for personidentifiserbare data", HostLinks.PersonalDataGuideHref, false),
             ("Veileder for anonyme, aggregerte data", HostLinks.AnonymousDataGuideHref, false)],
            offer.Links.Select(l => (l.Text, (string?)l.Href, l.Primary)));
        Assert.Equal(
            "Søknaden behandles av Helsedataservice. Selve opplysningene får du fra St. Olavs hospital HF. Les søknadsveilederen før du søker.",
            Sentence(offer));
    }

    [Fact]
    public void ViaHelsedata_WhenTheHostGaveNoLinks_ThenNothing_AndNotTheDirectRoute()
    {
        Assert.Empty(Offers(Kilde("""["soknad-via-helsedata","tilgang-krever-soknad"]""", ("SoknadOmTilgangUrl", "https://example.org/sok")),
            new KildeAccessLinks()));
    }

    [Fact]
    public void Direct_WithAnApplicationPage_ThenItIsTheButton()
    {
        var offer = Assert.Single(Offers(Kilde("""["tilgang-krever-soknad"]""",
            ("SoknadOmTilgangUrl", "https://example.org/sok"), ("SoknadOmTilgangEpost", "data@example.org"), ("Hjemmeside", "https://example.org"))));

        Assert.Equal(("Søk om tilgang hos registeret", "https://example.org/sok"), (offer.Links.Single().Text, offer.Links.Single().Href));
        Assert.Equal("Du søker om tilgang direkte hos Testregisteret. Se registerets egen side for krav og framgangsmåte.", Sentence(offer));
    }

    [Fact]
    public void Direct_WithOnlyAnAddress_ThenMailto()
    {
        var offer = Assert.Single(Offers(Kilde("""["tilgang-krever-soknad"]""", ("soknadomtilgangepost", "data@example.org"))));

        Assert.Equal(("Send e-post", "mailto:data@example.org"), (offer.Links.Single().Text, offer.Links.Single().Href));
        Assert.Equal("Du søker om tilgang direkte hos Testregisteret. Send søknaden til data@example.org.", Sentence(offer));
    }

    [Theory]
    [InlineData("Hjemmeside", "www.example.org", "https://www.example.org")]
    [InlineData("landingPage", "https://example.org/register", "https://example.org/register")]
    public void Direct_WithNeither_ThenTheWebsite(string key, string value, string href)
    {
        var offer = Assert.Single(Offers(Kilde("""["tilgang-krever-soknad"]""", ("SoknadOmTilgangUrl", "javascript:alert(1)"), (key, value))));

        Assert.Equal(href, offer.Links.Single().Href);
        Assert.EndsWith("Se registerets nettsted for hvordan du søker.", Sentence(offer), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("mailto:x@example.org?subject=hei")]
    [InlineData("[Søk her](https://example.org/sok)")]
    public void Direct_ThePageIsAWebPage_AndMarkdownIsRead(string value)
    {
        var href = Offers(Kilde("""["tilgang-krever-soknad"]""", ("SoknadOmTilgangUrl", value), ("Hjemmeside", "https://example.org"))).Single().Links.Single().Href;
        Assert.Equal(value.StartsWith("mailto", StringComparison.Ordinal) ? "https://example.org" : "https://example.org/sok", href);
    }

    [Fact]
    public void Direct_AnAddressWithAPercent_IsNotLinked()
    {
        Assert.Empty(Offers(Kilde("""["tilgang-krever-soknad"]""", ("SoknadOmTilgangEpost", "a%0ABcc%3Ax@example.org"))));
    }

    [Fact]
    public void Direct_WithNothingToLinkTo_ThenNothing()
    {
        Assert.Empty(Offers(Kilde("""["tilgang-krever-soknad"]""", ("SoknadOmTilgangEpost", "not an address"))));
    }

    [Theory]
    [InlineData("""["apne-data"]""")]
    [InlineData("""["statistikkbank"]""")]
    public void OpenData_ThenItsOwnLink_ElseTheHostsPage(string levels)
    {
        Assert.Equal("https://norgeshelsa.no/", Offers(Kilde(levels, ("AapneDataUrl", "https://norgeshelsa.no/"))).Single().Links.Single().Href);

        var fallback = Offers(Kilde(levels)).Single();
        Assert.Equal((KildeAccess.OpenData, "/no/apne-data/"), (fallback.Level, fallback.Links.Single().Href));
        Assert.Equal("Statistikk fra Testregisteret er åpent tilgjengelig, uten søknad.", Sentence(fallback));
        Assert.Empty(Offers(Kilde(levels), new KildeAccessLinks()));
    }

    [Fact]
    public void BothKinds_ThenTheApplicationFirst_ThenOpenData()
    {
        Assert.Equal([KildeAccess.RequiresApplication, KildeAccess.OpenData],
            Offers(Kilde("""["apne-data","tilgang-krever-soknad"]""", ("Hjemmeside", "https://example.org"))).Select(o => o.Level));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[]")]
    public void NoLevel_ThenNothing(string? levels)
    {
        Assert.Empty(Offers(Kilde(levels!, ("SoknadOmTilgangUrl", "https://example.org/sok"), ("AapneDataUrl", "https://example.org/data"))));
    }

    [Fact]
    public void KildeView_DrawsTheOfferInTheActionRow_WithTheCataloguesWordsMarkedForAnEnglishReader()
    {
        var cut = Render<KildeView>(b => b
            .Add(c => c.Kilde, Kilde("""["soknad-via-helsedata"]"""))
            .Add(c => c.Language, "en")
            .Add(c => c.AccessLinks, HostLinks));

        var access = cut.Find(".munin-explorer-page__actions .munin-explorer-kilde__access");
        // After the name heading in document order, so the kilde is named before what to do about it.
        var heading = cut.Find("h1, h2, h3, h4, h5, h6");
        Assert.True(heading.CompareDocumentPosition(access).HasFlag(AngleSharp.Dom.DocumentPositions.Following));
        var button = access.QuerySelector("a.hd-button-square.button-square--primary")!;
        Assert.Equal(("Apply for access", HostLinks.ApplicationHref), (button.TextContent, button.GetAttribute("href")));
        Assert.Equal(3, access.QuerySelectorAll("a").Length);
        Assert.Equal("no", access.QuerySelector("span[lang]")!.GetAttribute("lang"));
        Assert.Contains("The data itself comes from St. Olavs hospital HF.", access.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void KildeView_AHostsOwnActions_KeepTheirRowBeforeTheName_AndTheAccessRowFollowsIt()
    {
        var cut = Render<KildeView>(b => b
            .Add(c => c.Kilde, Kilde("""["soknad-via-helsedata"]"""))
            .Add(c => c.AccessLinks, HostLinks)
            .Add(c => c.Actions, (Microsoft.AspNetCore.Components.RenderFragment)(r => r.AddMarkupContent(0, "<button type=\"button\">Lagre</button>"))));

        var rows = cut.FindAll(".munin-explorer-page__actions");
        var heading = cut.Find("h1, h2, h3, h4, h5, h6");
        Assert.Equal(2, rows.Count);
        Assert.Equal("Lagre", rows[0].TextContent.Trim());
        Assert.True(rows[0].CompareDocumentPosition(heading).HasFlag(AngleSharp.Dom.DocumentPositions.Following));
        Assert.NotNull(rows[1].QuerySelector(".munin-explorer-kilde__access"));
        Assert.True(heading.CompareDocumentPosition(rows[1]).HasFlag(AngleSharp.Dom.DocumentPositions.Following));
    }

    [Fact]
    public void KildeView_WithoutAParameter_TakesTheRegisteredLinks_AndWithNoLevelDrawsNoRow()
    {
        Services.AddSingleton(HostLinks);

        var cut = Render<KildeView>(b => b.Add(c => c.Kilde, Kilde("""["soknad-via-helsedata"]""")));
        Assert.Equal(HostLinks.ApplicationHref, cut.Find(".munin-explorer-kilde__access a").GetAttribute("href"));

        cut.Render(b => b.Add(c => c.Kilde, Kilde("[]")));
        Assert.Empty(cut.FindAll(".munin-explorer-page__actions"));
    }

    private static KildeAccessLinks Registered(Dictionary<string, string?> settings)
    {
        settings["MuninExplorer:ApiBaseUrl"] = "https://explorer.munin.skytest.fhi.no";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddMuninExplorer(configuration).BuildServiceProvider().GetRequiredService<KildeAccessLinks>();
    }

    [Fact]
    public void Configuration_WithNoSection_ThenHelsedatasPages()
    {
        Assert.Equal(new KildeAccessLinks(
            ApplicationHref: "/no/soknader/?opprettSoknad=min-oversikt",
            PersonalDataGuideHref: "/no/soknadsveiledning-oversikt/personidentifiserbare-opplysninger/",
            AnonymousDataGuideHref: "/no/soknadsveiledning-oversikt/sok-om-anonyme-data/",
            OpenDataHref: "/no/apne-data/"), Registered([]));
    }

    [Fact]
    public void Configuration_EachKeyOverridesOnlyItself_AndAnEmptyOneDropsItsLink()
    {
        var links = Registered(new()
        {
            ["MuninExplorer:AccessLinks:ApplicationHref"] = "/sok",
            ["MuninExplorer:AccessLinks:PersonalDataGuideHref"] = "",
        });

        Assert.Equal(KildeAccessLinks.Helsedata with { ApplicationHref = "/sok", PersonalDataGuideHref = "" }, links);
        Assert.DoesNotContain(Offers(Kilde("""["soknad-via-helsedata"]"""), links).Single().Links, l => l.Text.Contains("personidentifiserbare", StringComparison.Ordinal));
    }

    [Fact]
    public void Options_NullAccessLinks_RegistersNone()
    {
        var services = new ServiceCollection().AddMuninExplorer(o =>
        {
            o.ApiBaseUrl = "https://explorer.munin.skytest.fhi.no";
            o.AccessLinks = null;
        }).BuildServiceProvider();

        Assert.Null(services.GetService<KildeAccessLinks>());
    }
}
