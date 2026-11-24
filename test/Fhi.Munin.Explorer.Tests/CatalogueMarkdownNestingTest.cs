using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Markdig throws on blocks nested past its limit and accepts nested <c>![</c> our walk would overflow the stack
/// on; either way one stored value would end the circuit, or the process, for every reader (Fhi.Metadata-7vquc.8).</summary>
public class CatalogueMarkdownNestingTest : ExplorerTestContext
{
    private static readonly string DeepList = Lists(64);

    private static readonly string DeepQuote = string.Concat(Enumerable.Repeat("> ", 128)) + "a";

    private static readonly string DeepImages = string.Concat(Enumerable.Repeat("![", 9_990)) + "a";

    private static readonly string DeepBrackets = new string('[', 19_999) + "a";

    private static string Lists(int depth) => string.Concat(Enumerable.Repeat("- ", depth)) + "a";

    private static string Brackets(int count) => new string('[', count) + " [a](https://uit.no)";

    public static TheoryData<string> DeepTexts() => [DeepList, DeepQuote, DeepImages, DeepBrackets];

    public static TheoryData<string, bool> DeepTextsWithFlag()
    {
        var data = new TheoryData<string, bool>();

        foreach (var text in new[] { DeepList, DeepQuote, DeepImages, DeepBrackets })
        {
            data.Add(text, false);
            data.Add(text, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DeepTextsWithFlag))]
    public void Render_WhenMarkersNestTooDeep_ThenTheTextIsDrawnAsPlainText(string text, bool formatted)
    {
        var cut = Render(CatalogueMarkdown.Render(text, formatted));

        Assert.Empty(cut.FindAll("*"));
        Assert.Equal(text, string.Concat(cut.Nodes.Select(node => node.TextContent)));
    }

    [Theory]
    [MemberData(nameof(DeepTextsWithFlag))]
    public void Of_WhenMarkersNestTooDeep_ThenItKeepsItsElementAroundPlainText(string text, bool formatted)
    {
        Services.AddSingleton(new CatalogueFormattingSwitch(formatted));

        var cut = Render(CatalogueText.Of(text, "p"));

        Assert.Single(cut.FindAll("*"));
        Assert.Equal(text, cut.Find("p").TextContent);
    }

    [Theory]
    [MemberData(nameof(DeepTexts))]
    public void Link_WhenMarkersNestTooDeep_ThenItIsNoLink(string text)
    {
        Assert.Null(CatalogueMarkdown.Link(text));
    }

    [Theory]
    [MemberData(nameof(DeepTexts))]
    public void DrawsList_WhenMarkersNestTooDeep_ThenItIsFalse(string text)
    {
        Assert.False(CatalogueMarkdown.DrawsList(text, formatted: true));
    }

    [Theory]
    [MemberData(nameof(DeepTexts))]
    public void TopBlocks_WhenMarkersNestTooDeep_ThenItHasNoBlocks(string text)
    {
        Assert.Empty(CatalogueMarkdown.TopBlocks(text).Blocks);
    }

    [Theory]
    [MemberData(nameof(DeepTexts))]
    public void EmphasisSpans_WhenMarkersNestTooDeep_ThenItHasNoSpans(string text)
    {
        Assert.Empty(CatalogueMarkdown.EmphasisSpans(text));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Split_WhenAShortTextNestsTooDeep_ThenTheWholeTextIsTheLead(bool formatted)
    {
        var text = DeepList + "\n\n" + DeepQuote;

        Assert.True(text.Length < DescriptionLead.Limit);
        Assert.Equal((text, null), DescriptionLead.Split(text, formatted));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Split_WhenALongTextNestsTooDeepInALaterParagraph_ThenNoShallowHalfIsSplitOff(bool formatted)
    {
        var text = "- a\n- b\n\n" + string.Concat(Enumerable.Repeat("Dette er en setning. ", 30)) + "\n\n" + DeepQuote;

        Assert.Equal((text, null), DescriptionLead.Split(text, formatted));
    }

    [Theory]
    [InlineData(62, true)]
    [InlineData(63, false)]
    public void Render_WhenListsNestAtTheCap_ThenOnlyThoseUnderItDrawAsLists(int depth, bool drawn)
    {
        Assert.Equal(drawn, Render(CatalogueMarkdown.Render(Lists(depth), formatted: true)).FindAll("ul").Count > 0);
    }

    [Theory]
    [InlineData(125, 1)]
    [InlineData(126, 0)]
    public void TopBlocks_WhenQuotesNestAtTheCap_ThenOnlyThoseUnderItAreParsed(int depth, int blocks)
    {
        var text = string.Concat(Enumerable.Repeat("> ", depth)) + "a";

        Assert.Equal(blocks, CatalogueMarkdown.TopBlocks(text).Blocks.Count);
    }

    [Theory]
    [InlineData(124, 1)]
    [InlineData(125, 0)]
    public void Render_WhenUnclosedBracketsNestAtTheCap_ThenOnlyThoseUnderItKeepTheirLinks(int count, int anchors)
    {
        Assert.Equal(anchors, Render(CatalogueMarkdown.Render(Brackets(count), formatted: true)).FindAll("a").Count);
    }
}
