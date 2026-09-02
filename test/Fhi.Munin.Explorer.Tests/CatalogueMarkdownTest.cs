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
public class CatalogueMarkdownTest : BunitContext
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

        Assert.Contains("- Helseregisterloven", cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.FindAll("br").Count >= 2);
    }

    [Fact]
    public void Render_WhenTheTextExceedsTheCap_ThenItRendersAsPlainLinesWithoutParsing()
    {
        var text = "[x](https://uit.no) " + new string('a', CatalogueMarkdown.MaxParsedLength);

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
}
