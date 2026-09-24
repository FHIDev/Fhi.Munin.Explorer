using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The catalogue-text renderer: markdown links and line breaks render as elements, and everything
/// else stays literal text.
/// </summary>
/// <remarks>
/// Half of this class is a security pin rather than behaviour coverage. The renderer's guarantee
/// is that catalogue text — editable master data reaching helsedata.no — can only ever produce an
/// anchor or a break, whatever it holds (FHIDev/Munin#5385). That guarantee lives in what the
/// walker has no case for, so a later "upgrade" to a fuller markdown pipeline would loosen it
/// without failing any behaviour test. The raw-HTML, <c>javascript:</c> and heading tests below
/// are what make that loosening fail loudly instead.
/// </remarks>
public class CatalogueMarkdownTest : ExplorerTestContext
{
    private IRenderedComponent<IComponent> Rendered(string text) => Render(CatalogueMarkdown.Render(text));

    [Fact]
    public void Render_WhenTheTextCarriesAMarkdownLink_ThenItBecomesAGuardedAnchor()
    {
        var cut = Rendered("Se [Tromsøundersøkelsen](https://uit.no/research/tromsostudy) for mer.");

        var anchor = cut.Find("a");

        Assert.Equal("https://uit.no/research/tromsostudy", anchor.GetAttribute("href"));
        Assert.Equal("noopener noreferrer", anchor.GetAttribute("rel"));
        Assert.Equal("Tromsøundersøkelsen", anchor.TextContent);
    }

    [Fact]
    public void Render_WhenTheLabelRepeatsTheUrl_ThenTheAnchorStillRenders()
    {
        // The shape Hjemmeside actually arrives in: [https://uit.no/...](https://uit.no/...).
        var cut = Rendered("[https://uit.no/research/tromsostudy](https://uit.no/research/tromsostudy)");

        Assert.Equal("https://uit.no/research/tromsostudy", cut.Find("a").TextContent);
    }

    [Fact]
    public void Render_WhenTheTextCarriesBrTagsAndBareNewlines_ThenBothBecomeBreaks()
    {
        // 46 of 66 kilder separate paragraphs with plain newlines and 5 with <br>; both were
        // invisible or literal on screen before this renderer existed.
        var cut = Rendered("Første avsnitt.<br>Andre avsnitt.\r\nTredje avsnitt.");

        Assert.Equal(2, cut.FindAll("br").Count);
        Assert.DoesNotContain("&lt;br&gt;", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_WhenParagraphsAreSeparatedByABlankLine_ThenTheGapSurvives()
    {
        var cut = Rendered("Første avsnitt.\n\nAndre avsnitt.");

        Assert.Equal(2, cut.FindAll("br").Count);
        Assert.Contains("Første avsnitt.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Andre avsnitt.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenTheHrefSchemeIsNotAllowed_ThenTheLinkStaysLiteralText()
    {
        var cut = Rendered("[klikk her](javascript:alert(1))");

        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("[klikk her](javascript:alert(1))", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "script")]
    [InlineData("<img src=x onerror=alert(1)>", "img")]
    [InlineData("<a href=\"https://evil.example\">lenke</a>", "a")]
    public void Render_WhenTheTextIsRawHtml_ThenNoElementRendersAndTheTagShowsAsText(
        string text, string element)
    {
        var cut = Rendered(text);

        Assert.Empty(cut.FindAll(element));
        Assert.Contains("&lt;", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenTheTextUsesConstructsWithoutACase_ThenTheSourceShowsLiterally()
    {
        // Emphasis and headings are outside the decided grammar: a heading would fight the host
        // page's outline. Literal source is what these fields showed before, so nothing is lost.
        var headed = Rendered("# Overskrift");
        var bold = Rendered("**Lovverk**");

        Assert.Empty(headed.FindAll("h1"));
        Assert.Contains("# Overskrift", headed.Markup, StringComparison.Ordinal);
        Assert.Contains("**Lovverk**", bold.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenABulletListCarriesALinklessLine_ThenEachLineKeepsItsBreak()
    {
        var cut = Rendered("Lovverk:\n- Helseregisterloven\n- Personopplysningsloven");

        Assert.Equal("Lovverk:<br /><br />- Helseregisterloven<br />- Personopplysningsloven", cut.Markup);
    }

    [Fact]
    public void Render_WhenAListItemCarriesALink_ThenTheLinkIsLiveAndTheMarkerStaysLiteral()
    {
        // K_KK's Kvalitetsnote shape: the links sit inside list items, not in a paragraph.
        var cut = Rendered("Kilder:\n- Se [veilederen](https://example.org/v)\n- Annet");

        var anchor = Assert.Single(cut.FindAll("a"));

        Assert.Equal("https://example.org/v", anchor.GetAttribute("href"));
        Assert.Equal("veilederen", anchor.TextContent);
        Assert.Contains("- Se ", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("<br />- Annet", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("](https://", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("- first\n\n  second", "- first<br /><br />second")]
    [InlineData("- first\n  second", "- first<br />second")]
    [InlineData("- first\n  - nested\n\n  after", "- first<br />- nested<br /><br />after")]
    public void Render_WhenAListItemHoldsTwoBlocks_ThenTheBlankLineBetweenThemSurvives(string text, string markup)
    {
        Assert.Equal(markup, Rendered(text).Markup);
    }

    [Theory]
    [InlineData("[lovdata.no](https://lovdata.no)")]
    [InlineData("[lovdata.no](http://lovdata.no)")]
    [InlineData("https://lovdata.no")]
    public void Prose_WhenTheLabelIsOnlyItsOwnAddress_ThenItIsNotProseWhateverTheScheme(string value)
    {
        Assert.False(CatalogueMarkdown.Prose(value));
        Assert.True(CatalogueMarkdown.Prose("[Helseregisterloven](http://lovdata.no)"));
    }

    [Fact]
    public void Render_WhenAListIsNumberedAndLoose_ThenItsMarkersAndBlankLinesSurvive()
    {
        var cut = Rendered("1. Første\n\n2. Andre");

        Assert.Equal("1. Første<br /><br />2. Andre", cut.Markup);
    }

    [Theory]
    [InlineData("- [klikk](javascript:alert(1))")]
    [InlineData("- <script>alert(1)</script>")]
    [InlineData("- # Overskrift")]
    public void Render_WhenAListItemCarriesHostileInput_ThenItStaysText(string hostile)
    {
        // Walking list items must not open a way round the guarantee the class exists for.
        var cut = Rendered(hostile);

        Assert.Empty(cut.FindAll("a, script, h1"));
        Assert.Equal(hostile, cut.Nodes.Select(n => n.TextContent).Aggregate(string.Concat));
    }

    [Theory]
    [InlineData("x [a\nb", "x [a<br />b")]
    [InlineData("- [abc def\n- b", "- [abc def<br />- b")]
    [InlineData("- ![a\n- b", "- ![a<br />- b")]
    public void Render_WhenABracketNeverCloses_ThenNoTextAfterItIsLost(string text, string markup)
    {
        Assert.Equal(markup, Rendered(text).Markup);
    }

    [Fact]
    public void Render_WhenALinkIsReferenceStyle_ThenItRendersOnceAndItsDefinitionIsNotDrawn()
    {
        // K_MSIS's criteria are written this way; the definition must lend the link its URL and
        // draw nothing of its own.
        var cut = Rendered("Se [MSIS-forskriften].\n\n[MSIS-forskriften]: https://lovdata.no/msis");

        Assert.Equal(
            "Se <a href=\"https://lovdata.no/msis\" rel=\"noopener noreferrer\">MSIS-forskriften</a>.",
            cut.Markup);
    }

    [Fact]
    public void Render_WhenAReferenceDefinitionHasADisallowedScheme_ThenTheLabelStaysText()
    {
        var cut = Rendered("Se [ref].\n\n[ref]: javascript:alert(1)");

        Assert.Equal("Se [ref].<br /><br />[ref]: javascript:alert(1)", cut.Markup);
    }

    [Theory]
    [InlineData("Tekst\n\n[Kilde]: https://fhi.no", "Tekst<br /><br />[Kilde]: https://fhi.no")]
    [InlineData("Tekst\n\n[Merk]: Foreløpig", "Tekst<br /><br />[Merk]: Foreløpig")]
    [InlineData("Se [1].\n\n[1]: www.lovdata.no", "Se [1].<br /><br />[1]: www.lovdata.no")]
    [InlineData("*se [a]*\n\n[a]: https://x.no", "*se [a]*<br /><br />[a]: https://x.no")]
    [InlineData("[a]: Først\n\nMidt\n\n[b]: Sist", "[a]: Først<br /><br />Midt<br /><br />[b]: Sist")]
    public void Render_WhenNoDrawnAnchorTakesADefinitionsUrl_ThenTheDefinitionIsDrawnAsItsSource(
        string text, string markup)
    {
        // Unused, not an address, a scheme the anchor refuses, or cited only from literal text:
        // hiding any of them would lose what the curator wrote.
        Assert.Equal(markup, Rendered(text).Markup);
    }

    [Theory]
    [InlineData("Tekst\n\n[a]: https://x.no\nmer tekst", "Tekst<br /><br />[a]: https://x.no<br /><br />mer tekst")]
    [InlineData("[a]: https://x.no\n[b]: https://y.no\nTekst", "[a]: https://x.no<br /><br />[b]: https://y.no<br /><br />Tekst")]
    [InlineData("> [a]: https://x.no\n\nTekst", "&gt; [a]: https://x.no<br /><br />Tekst")]
    [InlineData("- [a]: https://x.no\n- Tekst", "- [a]: https://x.no<br />- Tekst")]
    [InlineData("1. [a]: https://x.no", "1. [a]: https://x.no")]
    [InlineData("- [a]: https://x.no\n\n  Tekst", "- [a]: https://x.no<br /><br />  Tekst")]
    public void Render_WhenAnUnusedDefinitionSitsInsideOtherText_ThenItIsDrawnOnceWhereItWasWritten(
        string text, string markup)
    {
        // Markdig lifts every definition out to one group; a source slice may already show it.
        Assert.Equal(markup, Rendered(text).Markup);
    }

    [Fact]
    public void Render_WhenADefinitionComesBeforeItsLink_ThenTheLinkStillTakesItAndNothingElseIsDrawn()
    {
        Assert.Equal("Se <a href=\"https://x.no\" rel=\"noopener noreferrer\">a</a>.",
                     Rendered("[a]: https://x.no\n\nSe [a].").Markup);
    }

    [Fact]
    public void Render_WhenAListItemsTextStartsOnTheNextLine_ThenTheBreakAfterTheMarkerSurvives()
    {
        Assert.Equal("-<br />  foo<br />- b", Rendered("-\n  foo\n- b").Markup);
    }

    [Fact]
    public void Render_WhenTheTextExceedsTheCap_ThenItRendersAsPlainLinesWithoutParsing()
    {
        var text = "[x](https://uit.no) " + new string('a', CatalogueMarkdown.MaxParsedLength);

        var cut = Rendered(text);

        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("[x](https://uit.no)", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenBrTagsCarryTheTextOverTheCap_ThenItStillRendersAsPlainLines()
    {
        // Rewriting <br> shortens the value, so a cap measured after it would let this through.
        var text = "[x](https://uit.no)"
            + string.Concat(Enumerable.Repeat("<br />", CatalogueMarkdown.MaxParsedLength / 3));

        var cut = Rendered(text);

        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("[x](https://uit.no)", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[UiT](https://uit.no/forskning)", "UiT", "https://uit.no/forskning")]
    [InlineData("[https://uit.no](https://uit.no)", "https://uit.no", "https://uit.no")]
    [InlineData("https://uit.no/research", "https://uit.no/research", "https://uit.no/research")]
    [InlineData("mailto:post@fhi.no", "mailto:post@fhi.no", "mailto:post@fhi.no")]
    [InlineData("www.barnediabetes.no", "www.barnediabetes.no", "https://www.barnediabetes.no")]
    public void Link_WhenTheValueIsOneAllowedLink_ThenLabelAndHrefResolve(
        string raw, string label, string href)
    {
        Assert.Equal((label, href), CatalogueMarkdown.Link(raw));
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("ftp://uit.no/fil")]
    [InlineData("Hjemmesiden er [UiT](https://uit.no)")]
    [InlineData("barnediabetes.no")]
    [InlineData("bare prosa")]
    [InlineData("")]
    [InlineData(null)]
    public void Link_WhenTheValueIsNotOneAllowedLink_ThenItStaysText(string? raw)
    {
        Assert.Null(CatalogueMarkdown.Link(raw));
    }

    [Fact]
    public void LinkList_WhenEveryPartIsAnAddress_ThenEachIsReturnedInOrder()
    {
        Assert.Equal(
            ["http://data.europa.eu/eli/reg/2025/327/oj", "https://lovdata.no/eli/lov/2001-05-18-24",
             "https://lovdata.no/eli/forskrift/2018-04-27-645"],
            CatalogueMarkdown.LinkList("http://data.europa.eu/eli/reg/2025/327/oj;https://lovdata.no/eli/lov/2001-05-18-24;"
                                       + "https://lovdata.no/eli/forskrift/2018-04-27-645"));
    }

    [Fact]
    public void LinkList_WhenATrailingSemicolonAndWhitespaceSurroundTheParts_ThenTheyAreTolerated()
    {
        Assert.Equal(["https://a.example/", "https://b.example/x"],
                     CatalogueMarkdown.LinkList(" https://a.example/ ; https://b.example/x ; "));
    }

    [Theory]
    [InlineData("https://lovdata.no/eli/lov/2001-05-18-24")]
    [InlineData("https://lovdata.no/eli/lov/2001-05-18-24;")]
    [InlineData("https://x.example/p?a=1;b=2")]
    [InlineData("https://a.example;not a url")]
    [InlineData("a;b")]
    [InlineData("https://a.example;mailto:post@fhi.no")]
    [InlineData("https://a.example;ftp://b.example")]
    [InlineData(null)]
    public void LinkList_WhenTheValueIsNotSeveralAddresses_ThenItIsNull(string? raw)
    {
        // A ';' inside one address's query splits into a part that is no address, which is what keeps it whole.
        Assert.Null(CatalogueMarkdown.LinkList(raw));
    }

    [Theory]
    [InlineData("https://lovdata.no/eli/lov/2001-05-18-24;", "https://lovdata.no/eli/lov/2001-05-18-24")]
    [InlineData(" https://lovdata.no/eli/lov/2001-05-18-24 ; ", "https://lovdata.no/eli/lov/2001-05-18-24")]
    [InlineData("https://x.example/p?a=1;b=2", "https://x.example/p?a=1;b=2")]
    public void Link_WhenASemicolonDoesNotMakeAList_ThenTheValueIsStillOneLink(string raw, string href)
    {
        Assert.Equal((href, href), CatalogueMarkdown.Link(raw));
    }

    [Fact]
    public void Link_WhenTheValueIsSeveralAddresses_ThenItIsNotOneLink()
    {
        // Linked whole, the list is an address that exists nowhere (Fhi.Metadata-61s28).
        const string value = "https://a.example/;https://b.example/";

        Assert.Null(CatalogueMarkdown.Link(value));
        Assert.False(CatalogueMarkdown.Prose(value));
        Assert.Equal(value, CatalogueMarkdown.Words(value));
    }
}
