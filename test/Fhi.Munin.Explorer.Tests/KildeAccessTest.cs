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
    private static readonly KildeAccessLinks HostLinks = new(
        ApplicationHref: "/no/soknader/?opprettSoknad=min-oversikt",
        PersonalDataGuideHref: "/no/soknadsveiledning-oversikt/personidentifiserbare-opplysninger/",
        AnonymousDataGuideHref: "/no/soknadsveiledning-oversikt/sok-om-anonyme-data/",
        OpenDataHref: "/no/apne-data/");

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
        var button = access.QuerySelector("a.hd-button-square.button-square--primary")!;
        Assert.Equal(("Apply for access", HostLinks.ApplicationHref), (button.TextContent, button.GetAttribute("href")));
        Assert.Equal(3, access.QuerySelectorAll("a").Length);
        Assert.Equal("no", access.QuerySelector("span[lang]")!.GetAttribute("lang"));
        Assert.Contains("The data itself comes from St. Olavs hospital HF.", access.TextContent, StringComparison.Ordinal);
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

    [Fact]
    public void Configuration_TheAccessLinksSection_IsRegistered_AndWithoutItNothingIs()
    {
        var configured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MuninExplorer:ApiBaseUrl"] = "https://explorer.munin.skytest.fhi.no",
            ["MuninExplorer:AccessLinks:ApplicationHref"] = "/no/soknader/?opprettSoknad=min-oversikt",
            ["MuninExplorer:AccessLinks:OpenDataHref"] = "/no/apne-data/",
        }).Build();
        var links = new ServiceCollection().AddMuninExplorer(configured).BuildServiceProvider().GetRequiredService<KildeAccessLinks>();
        Assert.Equal(new KildeAccessLinks(ApplicationHref: "/no/soknader/?opprettSoknad=min-oversikt", OpenDataHref: "/no/apne-data/"), links);

        var bare = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MuninExplorer:ApiBaseUrl"] = "https://explorer.munin.skytest.fhi.no",
        }).Build();
        Assert.Null(new ServiceCollection().AddMuninExplorer(bare).BuildServiceProvider().GetService<KildeAccessLinks>());
    }
}
