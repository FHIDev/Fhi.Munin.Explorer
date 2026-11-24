using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Catalogue text with <c>CatalogueFormatting</c> on (Fhi.Metadata-7vquc.3, FHIDev/Munin#7449).</summary>
/// <remarks>The element-set test is the security pin: the decision allowed emphasis and lists only, so any further
/// element catalogue text can draw must fail there first.</remarks>
public class CatalogueFormattingTest : ExplorerTestContext
{
    private static readonly HashSet<string> AllowedElements =
        new(["a", "br", "strong", "em", "ul", "ol", "li"], StringComparer.OrdinalIgnoreCase);

    /// <summary>V_MFR.BARNETS_HELSE's Kommentar as it is stored, shortened.</summary>
    internal const string BarnetsHelseKommentar =
        "Innholdet i kodestrengen vil begrenses til det som er nødvendig for å oppnå formålet, i tråd med prinsippet "
        + "om dataminimering gitt av personvernforordningen (GDPR) artikkel 5.\n\n"
        + "En kodestreng inneholder en eller flere **kodeblokker** som er skilt med semikolon. Hver kodeblokk "
        + "inneholder 4 elementer som er skilt med underscore «_»: <br>\n"
        + "1. Element viser hvilken skjematype koden er rapportert på, f.eks. føde- eller barneavdelingsskjema\n"
        + "2. Element viser koden, f.eks. en diagnose- eller medikamentkode\n"
        + "3. Element viser kodegruppe, som brukes til å plassere en kode i en gitt kontekst\n"
        + "4. Element viser kodeverket koden tilhører, f.eks. ICD-10 eller ATC <br><br>\n\n"
        + "Forkortelser benyttet for **skjematype** :<br>\n"
        + "- FI: Fødeinstitusjon\n"
        + "- BA: Barneavdeling\n"
        + "- A:  Nemndbehandlet abort\n"
        + "- AN: Annet <br><br>\n\n"
        + "Forkortelser benyttet for **kodegrupper**:<br>\n"
        + "- ig: Inngrep (brukt for variabel intervensjon)\n"
        + "- io: Indikasjon for overflytting (brukt for variabel barnets_helse)";

    private IRenderedComponent<IComponent> Formatted(string text) =>
        Render(CatalogueMarkdown.Render(text, formatted: true));

    private static string List(string tag, params string[] items) =>
        $"<{tag} class=\"{CatalogueMarkdown.ListClass}\">{string.Concat(items.Select(item => $"<li>{item}</li>"))}</{tag}>";

    [Fact]
    public void Render_WhenFormattedTextCarriesStarEmphasis_ThenItIsStrongAndEm()
    {
        Assert.Equal("Se <strong>Lovverk</strong> og <em>kursiv</em>.",
                     Formatted("Se **Lovverk** og *kursiv*.").Markup);
    }

    [Theory]
    [InlineData("__fet__ og _kursiv_")]
    [InlineData("barnets_helse og mors_helse_foer")]
    public void Render_WhenFormattedTextCarriesUnderscores_ThenTheyStayLiteral(string text)
    {
        var cut = Formatted(text);

        Assert.Empty(cut.FindAll("strong, em"));
        Assert.Equal(text, cut.Markup);
    }

    [Fact]
    public void Render_WhenEmphasisHoldsALink_ThenTheAnchorIsInsideTheStrong()
    {
        var anchor = Formatted("**Se [veilederen](https://example.org/v)**").Find("strong a");

        Assert.Equal("https://example.org/v", anchor.GetAttribute("href"));
    }

    [Theory]
    [InlineData("Faktor a*b og c* til slutt.")]
    [InlineData("*Ledd b og c*d til slutt.")]
    public void Render_WhenOnlyOneEndOfTheEmphasisIsInsideAWord_ThenTheTextStaysLiteral(string text)
    {
        var cut = Formatted(text);

        Assert.Empty(cut.FindAll("em"));
        Assert.Equal(text, cut.Markup);
    }

    [Theory]
    [InlineData("Verdi x**2 + y**2 her.")]
    [InlineData("Ledd a**b**c her.")]
    [InlineData("Ledd ***a***b her.")]
    public void Render_WhenADoubleStarRunSitsInsideAWord_ThenTheTextStaysLiteral(string text)
    {
        var cut = Formatted(text);

        Assert.Empty(cut.FindAll("strong, em"));
        Assert.Equal(text, cut.Markup);
    }

    [Theory]
    [InlineData("3. c\n2. b\n1. a", "3", "2", "1")]
    [InlineData("1. a\n1. b\n1. c", "1", "1", "1")]
    [InlineData("0. a\n1. b\n2. c", "0", "1", "2")]
    public void Render_WhenANumberedListIsNotCountedUpFromItsStart_ThenEachItemShowsItsOwnNumber(
        string text, string first, string second, string third)
    {
        var list = Formatted(text).Find("ol");
        var number = int.Parse(list.GetAttribute("start") ?? "1");
        var drawn = new List<string>();

        // Counted the way a browser numbers an ol: start, then one up from the last, unless an li has a value.
        foreach (var item in list.Children)
        {
            number = item.GetAttribute("value") is { } value ? int.Parse(value) : number;
            drawn.Add(number.ToString());
            number++;
        }

        Assert.Equal([first, second, third], drawn);
    }

    [Fact]
    public void Render_WhenEmphasisHoldsAReferenceLink_ThenTheDefinitionIsNotDrawnAsWell()
    {
        var cut = Formatted("Se **[MSIS-forskriften]**.\n\n[MSIS-forskriften]: https://lovdata.no/msis");

        Assert.Equal("https://lovdata.no/msis", cut.Find("strong a").GetAttribute("href"));
        Assert.DoesNotContain("]:", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenFormattedTextHasABulletList_ThenItIsAUlRightAfterItsLeadIn()
    {
        Assert.Equal("Lovverk:" + List("ul", "Helseregisterloven", "Personopplysningsloven"),
                     Formatted("Lovverk:\n- Helseregisterloven\n- Personopplysningsloven").Markup);
    }

    [Fact]
    public void Render_WhenFormattedTextHasANumberedList_ThenItIsAnOl()
    {
        Assert.Equal(List("ol", "Første", "Andre"), Formatted("1. Første\n2. Andre").Markup);
    }

    [Fact]
    public void Render_WhenANumberedListStartsPastOne_ThenTheOlCarriesItsStart()
    {
        Assert.Equal("3", Formatted("3. Tredje\n4. Fjerde").Find("ol").GetAttribute("start"));
    }

    [Fact]
    public void Render_WhenTextFollowsAList_ThenNoBreakIsAddedBesideIt()
    {
        Assert.Equal("Før" + List("ul", "a") + "Etter", Formatted("Før\n\n- a\n\nEtter").Markup);
    }

    [Fact]
    public void Render_WhenAListNestsOneLevel_ThenTheInnerListIsInsideItsItem()
    {
        var cut = Formatted("- Ytre\n  - Indre\n- Neste");

        Assert.Equal("Indre", cut.Find("ul > li > ul > li").TextContent);
        Assert.Equal(2, cut.Find("ul").Children.Length);
    }

    [Fact]
    public void Render_WhenAListItemCarriesALink_ThenTheAnchorIsInsideTheItem()
    {
        var anchor = Formatted("Kilder:\n- Se [veilederen](https://example.org/v)\n- Annet").Find("li a");

        Assert.Equal("https://example.org/v", anchor.GetAttribute("href"));
    }

    [Theory]
    [InlineData("* a\n* b", "* a<br />* b")]
    [InlineData("+ a\n+ b", "+ a<br />+ b")]
    [InlineData("1) a\n2) b", "1) a<br />2) b")]
    public void Render_WhenAListUsesAnotherMarker_ThenItStaysLinesOfText(string text, string markup)
    {
        Assert.Equal(markup, Formatted(text).Markup);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "script")]
    [InlineData("<img src=x onerror=alert(1)>", "img")]
    [InlineData("<a href=\"https://evil.example\">lenke</a>", "a")]
    [InlineData("<strong>fet</strong>", "strong")]
    [InlineData("<ul><li>x</li></ul>", "ul")]
    public void Render_WhenFormattedTextIsRawHtml_ThenNoElementRendersAndTheTagShowsAsText(string text, string element)
    {
        var cut = Formatted(text);

        Assert.Empty(cut.FindAll(element));
        Assert.Contains("&lt;", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("# Overskrift", "h1")]
    [InlineData("`kode`", "code")]
    [InlineData("    innrykket kode", "pre")]
    [InlineData("> sitat", "blockquote")]
    public void Render_WhenFormattedTextHasAConstructWithoutACase_ThenItsSourceShowsLiterally(string text, string element)
    {
        var cut = Formatted(text);

        Assert.Empty(cut.FindAll(element));
        Assert.Contains(text.Trim(), string.Concat(cut.Nodes.Select(node => node.TextContent)), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenAFormattedLinkHasAJavascriptScheme_ThenItStaysLiteralText()
    {
        var cut = Formatted("- **[klikk her](javascript:alert(1))**");

        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("[klikk her](javascript:alert(1))", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenFormattedTextMixesEveryConstruct_ThenOnlyTheAllowedElementsAppear()
    {
        var cut = Formatted(BarnetsHelseKommentar + "\n\n# H\n\n<script>x</script>\n\n`c`\n\n> q\n\n"
                            + "[a](https://x.no) ![i](https://x.no/i.png) <b>b</b> ~~s~~ | t |\n\n---\n\n* p\n\n1) q");

        // Inside an anchor is CatalogueLink's own markup, the same whether or not the text is formatted.
        var drawn = cut.FindAll("*")
            .Where(element => element.ParentElement?.Closest("a") is null)
            .Select(element => element.TagName.ToLowerInvariant())
            .Distinct()
            .ToList();

        Assert.All(drawn, tag => Assert.Contains(tag, AllowedElements));
    }

    [Fact]
    public void Render_WhenTheKommentarOf7449IsFormatted_ThenBoldAndBothListKindsRenderAndNoBrShowsAsText()
    {
        var cut = Formatted(BarnetsHelseKommentar);

        Assert.Equal(["kodeblokker", "skjematype", "kodegrupper"], cut.FindAll("strong").Select(s => s.TextContent));
        Assert.Equal(4, cut.Find("ol").Children.Length);
        Assert.Equal([4, 2], cut.FindAll("ul").Select(ul => ul.Children.Length));
        Assert.DoesNotContain("**", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("&lt;br", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("barnets_helse", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenTheKommentarOf7449IsNotFormatted_ThenItsMarkersStayLiteralAsBefore()
    {
        var cut = Render(CatalogueMarkdown.Render(BarnetsHelseKommentar));

        Assert.Empty(cut.FindAll("strong, em, ul, ol, li"));
        Assert.Contains("**kodeblokker**", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("1. Element", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("- a", true, true)]
    [InlineData("Før\n1. a", true, true)]
    [InlineData("- a", false, false)]
    [InlineData("* a", true, false)]
    [InlineData("**fet**", true, false)]
    [InlineData("", true, false)]
    [InlineData("> - a", true, false)]
    [InlineData("-", true, false)]
    public void DrawsList_WhenAskedForAText_ThenItAnswersWhetherRenderDrawsAListElement(string text, bool formatted, bool expected)
    {
        Assert.Equal(expected, CatalogueMarkdown.DrawsList(text, formatted));
        Assert.Equal(expected, Render(CatalogueMarkdown.Render(text, formatted)).FindAll("ul, ol").Count > 0);
    }

    [Fact]
    public void DrawsList_WhenTheTextIsOverTheCap_ThenItIsNoWithoutParsing()
    {
        // Render draws plain lines past the cap, so a container must stay what it was.
        var text = "- a\n" + new string('x', CatalogueMarkdown.MaxParsedLength);

        Assert.False(CatalogueMarkdown.DrawsList(text, formatted: true));
        Assert.Empty(Render(CatalogueMarkdown.Render(text, formatted: true)).FindAll("ul"));
    }

    [Theory]
    [InlineData("-", "-")]
    [InlineData("- ", "- ")]
    [InlineData("1.", "1.")]
    [InlineData("- a\n-", "- a<br />-")]
    public void Render_WhenAListHasAnEmptyItem_ThenItStaysTextRatherThanAnEmptyBullet(string text, string markup)
    {
        // A lone "-" is the catalogue's placeholder for "nothing", and an empty bullet would hide it.
        Assert.Equal(markup, Formatted(text).Markup);
    }

    [Fact]
    public void Render_WhenListsNestPastThreeDeep_ThenTheFourthStaysLinesOfText()
    {
        var cut = Formatted("- 1\n  - 2\n    - 3\n      - 4");

        Assert.Equal(3, cut.FindAll("ul").Count);
        Assert.Contains("- 4", cut.Find("ul ul ul li").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenEmphasisNestsPastThreeDeep_ThenTheFourthStaysText()
    {
        var cut = Formatted("**a *b **c *d* c** b* a**");

        Assert.Equal(3, cut.FindAll("strong, em").Count);
        Assert.Contains("*d*", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenALinksLabelIsBold_ThenTheStrongIsInsideTheAnchor()
    {
        Assert.Equal("x", Formatted("[**x**](https://example.org)").Find("a strong").TextContent);
    }

    [Fact]
    public void Render_WhenABracketNeverClosesBeforeBold_ThenTheBracketStaysAndTheBoldRenders()
    {
        var cut = Formatted("[uten slutt **fet**");

        Assert.Equal("fet", cut.Find("strong").TextContent);
        Assert.StartsWith("[uten slutt ", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("- <b>x</b>", "b")]
    [InlineData("**`kode`**", "code")]
    [InlineData("- ![i](https://x.no/i.png)", "img")]
    [InlineData("- # h", "h1")]
    [InlineData("- > q", "blockquote")]
    public void Render_WhenAConstructWithoutACaseSitsInsideAnItemOrEmphasis_ThenItStaysText(string text, string element)
    {
        var cut = Formatted(text);

        Assert.Empty(cut.FindAll(element));
        Assert.NotEmpty(cut.FindAll("li, strong"));
    }

    [Theory]
    [InlineData(true, "div")]
    [InlineData(false, "p")]
    public void Of_WhenAParagraphsTextHoldsAList_ThenTheContainerIsADivOnlyWithTheFlagOn(bool on, string tag)
    {
        Services.AddSingleton(new CatalogueFormattingSwitch(on));

        var cut = Render(CatalogueText.Of("Liste:\n- a", "p", "ingress", "no"));

        var container = cut.Find(".ingress");

        Assert.Equal(tag, container.TagName.ToLowerInvariant());
        Assert.Equal("no", container.GetAttribute("lang"));
    }

    [Fact]
    public void Of_WhenTheFlagIsOnAndTheTextHoldsNoList_ThenTheContainerStaysItsOwnElement()
    {
        Services.AddSingleton(new CatalogueFormattingSwitch(true));

        var cut = Render(CatalogueText.Of("Bare **fet**.", "span", lang: "no"));

        Assert.Equal("<span lang=\"no\">Bare <strong>fet</strong>.</span>", cut.Markup);
    }

    [Fact]
    public void Of_WhenNoSwitchIsRegistered_ThenTheTextIsNotFormatted()
    {
        var cut = Render(CatalogueText.Of("**fet**\n- a"));

        Assert.Equal("**fet**<br /><br />- a", cut.Markup);
    }
}
