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
            // Tilgangsnivå is placed in Tilgang og ansvar (Munin 0056), which is where the block is explained.
            PropertyMetadata =
            [
                new PropertyMetadataEntry
                {
                    Key = AccessLevels.Key,
                    SortOrder = 10,
                    GroupKey = KildeView.AccessGroupKey,
                    GroupTranslations = new Dictionary<string, string> { ["no"] = "Tilgang og ansvar" },
                    DisplayNameTranslations = new Dictionary<string, string> { ["no"] = "Tilgangsnivå" },
                },
            ],
            Sections =
            [
                new SectionPlacement
                {
                    Key = KildeView.AccessGroupKey,
                    SortOrder = 1,
                    Translations = new Dictionary<string, string> { ["no"] = "Tilgang og ansvar", ["en"] = "Access and responsibility" },
                },
            ],
        };
    }

    private static IReadOnlyList<KildeAccess.Offer> Offers(KildeDetail kilde, KildeAccessLinks? links = null) =>
        KildeAccess.Offers(kilde, (links ?? HostLinks).For("no"), Texts.For("no"));

    private static string Sentence(KildeAccess.Offer offer) =>
        string.Concat(KildeAccess.Pieces(offer).Select(p => p.Text + p.Fill?.Text));

    [Fact]
    public void ViaHelsedata_ThenTheFormFirst_ThenBothGuides_AndTheForvalterIsNamed()
    {
        var offer = Assert.Single(Offers(Kilde("""["soknad-via-helsedata","tilgang-krever-soknad"]""")));

        Assert.Equal(KildeAccess.ViaHelsedata, offer.Level);
        Assert.Equal(
            [("Søk om tilgang", "/no/soknader/?opprettSoknad=min-oversikt", true),
             ("Veileder for personidentifiserbare data", "/no/soknadsveiledning-oversikt/personidentifiserbare-opplysninger/", false),
             ("Veileder for anonyme, aggregerte data", "/no/soknadsveiledning-oversikt/sok-om-anonyme-data/", false)],
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

    private IRenderedComponent<KildeView> RenderKilde(KildeDetail kilde, string language = "no") =>
        Render<KildeView>(b => b.Add(c => c.Kilde, kilde).Add(c => c.Language, language).Add(c => c.AccessLinks, HostLinks));

    private static AngleSharp.Dom.IElement AccessSection(IRenderedComponent<KildeView> cut) =>
        cut.FindAll("section").Single(section => section.QuerySelector(".munin-explorer-kilde__access") is not null);

    [Fact]
    public void KildeView_TheTopHasOnlyTheButton_AndTheWholeBlockOpensTilgangOgAnsvar()
    {
        // Robin, 2026-10-23: Skuld and the design pages have a button at the top, not the full text.
        var cut = Render<KildeView>(b => b
            .Add(c => c.Kilde, Kilde("""["soknad-via-helsedata"]""") with { TotalVariables = 3 })
            .Add(c => c.Language, "en")
            .Add(c => c.VariablesHref, "/en/variabler/?kilde=x")
            .Add(c => c.AccessLinks, HostLinks));

        var top = cut.Find(".munin-explorer-page__actions");
        Assert.Equal(["Apply for access"], top.QuerySelectorAll("a").Select(a => a.TextContent));
        Assert.Equal("/en/soknader/?opprettSoknad=min-oversikt", top.QuerySelector("a.hd-button-square.button-square--primary")!.GetAttribute("href"));
        Assert.Null(top.QuerySelector(".munin-explorer-kilde__access"));
        // One snapshot: the view renders again once the hierarchy loads, and nodes from two renders cannot be compared.
        var markup = cut.Markup;
        Assert.True(markup.IndexOf("Testregisteret</h2>", StringComparison.Ordinal)
                    < markup.IndexOf("munin-explorer-page__actions", StringComparison.Ordinal));
        // Above the jump links, as Skuld orders them (Robin, 2026-10-23).
        Assert.True(markup.IndexOf("munin-explorer-page__actions", StringComparison.Ordinal)
                    < markup.IndexOf("munin-explorer-kilde__jumps", StringComparison.Ordinal));

        var section = AccessSection(cut);
        var block = section.QuerySelector(".munin-explorer-kilde__access")!;
        Assert.Same(block.ParentElement, section.QuerySelector("h3 + *"));
        Assert.Equal(
            ["/en/soknader/?opprettSoknad=min-oversikt",
             "/en/soknadsveiledning-oversikt/personidentifiserbare-opplysninger/",
             "/en/soknadsveiledning-oversikt/sok-om-anonyme-data/"],
            block.QuerySelectorAll("a").Select(a => a.GetAttribute("href")));
        // helsedata.no's own «Søknad om tilgang til data» box, a white button with a grey border, not a second primary.
        var box = block.ParentElement!;
        Assert.Equal(["infobox", "infobox__datasources-block"], box.ClassList);
        Assert.NotNull(box.QuerySelector(":scope > .illustration.illustration_brand-sign[aria-hidden=true]"));
        Assert.Equal(["/en/soknader/?opprettSoknad=min-oversikt"],
            block.QuerySelectorAll("a.hd-button-square.button-square--tertiary").Select(a => a.GetAttribute("href")));
        Assert.Empty(block.QuerySelectorAll(".button-square--primary"));
        Assert.Equal("no", block.QuerySelector("span[lang]")!.GetAttribute("lang"));
        Assert.Contains("The data itself comes from St. Olavs hospital HF.", block.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void KildeView_WithNoTilgangOgAnsvarSection_TheBoxGetsASectionOfItsOwn()
    {
        // An older Munin, or a kilde with nothing placed there: the explanation must not vanish with the section.
        var cut = RenderKilde(Kilde("""["soknad-via-helsedata"]""") with { Sections = [], PropertyMetadata = [] });

        var own = cut.Find($"section#{DetailSectionIds.Access}");
        Assert.Equal("Tilgang og ansvar", own.QuerySelector("h3")!.TextContent);
        Assert.NotNull(own.QuerySelector(".infobox.infobox__datasources-block .munin-explorer-kilde__access"));
        Assert.Single(cut.FindAll(".munin-explorer-kilde__access"));
    }

    [Fact]
    public void KildeView_WithTheSection_ThereIsNoSecondOne()
    {
        var cut = RenderKilde(Kilde("""["soknad-via-helsedata"]"""));

        Assert.Empty(cut.FindAll($"section#{DetailSectionIds.Access}"));
        Assert.Single(cut.FindAll(".munin-explorer-kilde__access"));
    }

    [Theory]
    [InlineData("SoknadOmTilgangUrl", "https://example.org/sok", "https://example.org/sok")]
    [InlineData("SoknadOmTilgangEpost", "data@example.org", "mailto:data@example.org")]
    [InlineData("Hjemmeside", "https://example.org", "https://example.org")]
    public void KildeView_TheButtonSaysSokOmTilgang_AndGoesToTheRegistrysTarget(string key, string value, string href)
    {
        var button = RenderKilde(Kilde("""["tilgang-krever-soknad"]""", (key, value))).Find(".munin-explorer-page__actions a");

        Assert.Equal(("Søk om tilgang", href), (button.TextContent, button.GetAttribute("href")));
    }

    [Fact]
    public void KildeView_WithOnlyOpenData_TheButtonIsSeApneData()
    {
        var button = RenderKilde(Kilde("""["apne-data"]""")).Find(".munin-explorer-page__actions a");

        Assert.Equal(("Se åpne data", "/no/apne-data/"), (button.TextContent, button.GetAttribute("href")));
    }

    [Fact]
    public void KildeView_ApplicationAndOpenData_ThenTwoButtons_AndBothExplainedInTheSection()
    {
        var cut = RenderKilde(Kilde("""["soknad-via-helsedata","apne-data"]""", ("AapneDataUrl", "https://norgeshelsa.no/")));

        Assert.Equal([("Søk om tilgang", "hd-button-square button-square--primary"), ("Se åpne data", "hd-button-square button-square--tertiary")],
            cut.FindAll(".munin-explorer-page__actions a").Select(a => (a.TextContent, a.GetAttribute("class"))));
        Assert.Contains("https://norgeshelsa.no/", AccessSection(cut).QuerySelectorAll("a").Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void KildeView_AHostsOwnActions_KeepTheirRowBeforeTheName_AndTheButtonRowFollowsIt()
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
        Assert.Equal("Søk om tilgang", rows[1].QuerySelector("a")!.TextContent);
        Assert.True(heading.CompareDocumentPosition(rows[1]).HasFlag(AngleSharp.Dom.DocumentPositions.Following));
    }

    [Fact]
    public void KildeView_WithoutAParameter_TakesTheRegisteredLinks_AndWithNoLevelDrawsNoRow()
    {
        Services.AddSingleton(HostLinks);

        var cut = Render<KildeView>(b => b.Add(c => c.Kilde, Kilde("""["soknad-via-helsedata"]""")));
        Assert.Equal("/no/soknader/?opprettSoknad=min-oversikt", cut.Find(".munin-explorer-page__actions a").GetAttribute("href"));

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
        var links = Registered([]);
        Assert.Equal(KildeAccessLinks.Helsedata, links);
        Assert.Equal(new KildeAccessLinks(
            ApplicationHref: "/en/soknader/?opprettSoknad=min-oversikt",
            PersonalDataGuideHref: "/en/soknadsveiledning-oversikt/personidentifiserbare-opplysninger/",
            AnonymousDataGuideHref: "/en/soknadsveiledning-oversikt/sok-om-anonyme-data/",
            OpenDataHref: "/en/apne-data/"), links.For("en"));
        Assert.Equal("/no/apne-data/", links.For("no").OpenDataHref);
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
    public void Configuration_ALanguagesOwnKey_WinsForThatLanguageOnly_AndAnEmptyOneDropsItThere()
    {
        var links = Registered(new()
        {
            ["MuninExplorer:AccessLinks:ApplicationHref"] = "/{language}/sok",
            ["MuninExplorer:AccessLinks:en:ApplicationHref"] = "/english/apply",
            ["MuninExplorer:AccessLinks:en:PersonalDataGuideHref"] = "",
        });

        Assert.Equal(("/english/apply", ""), (links.For("en").ApplicationHref, links.For("en").PersonalDataGuideHref));
        Assert.Equal("/no/sok", links.For("no").ApplicationHref);
        Assert.Equal("/no/soknadsveiledning-oversikt/personidentifiserbare-opplysninger/", links.For("no").PersonalDataGuideHref);
        Assert.DoesNotContain(Offers(Kilde("""["soknad-via-helsedata"]"""), links.For("en")).Single().Links,
            l => l.Text.Contains("personidentifiserbare", StringComparison.Ordinal));
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
