using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Markdig throws on blocks nested past its limit and accepts nested <c>![</c> our walk would overflow the stack
/// on; either way one stored value would end the circuit, or the process, for every reader (Fhi.Metadata-7vquc.8).</summary>
public class CatalogueMarkdownNestingTest : ExplorerTestContext
{
    private static readonly string DeepList = string.Concat(Enumerable.Repeat("- ", 64)) + "a";

    private static readonly string DeepQuote = string.Concat(Enumerable.Repeat("> ", 128)) + "a";

    private static readonly string DeepImages = string.Concat(Enumerable.Repeat("![", 9_990)) + "a";

    public static TheoryData<string> DeepTexts() => [DeepList, DeepQuote, DeepImages];

    public static TheoryData<string, bool> DeepTextsWithFlag()
    {
        var data = new TheoryData<string, bool>();

        foreach (var text in new[] { DeepList, DeepQuote, DeepImages })
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
    public void CatalogueText_WhenMarkersNestTooDeep_ThenItKeepsItsElementAroundPlainText(string text, bool formatted)
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
    [MemberData(nameof(DeepTextsWithFlag))]
    public void DrawsList_WhenMarkersNestTooDeep_ThenItIsFalse(string text, bool formatted)
    {
        Assert.False(CatalogueMarkdown.DrawsList(text, formatted));
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
    public void Split_WhenALongTextNestsTooDeep_ThenItIsCutAsUnformattedTextIs(bool formatted)
    {
        var text = DeepList + " Slutt. " + string.Concat(Enumerable.Repeat("Dette er en setning. ", 40));

        var (lead, folded) = DescriptionLead.Split(text, formatted);

        Assert.Equal(DescriptionLead.Split(text, formatted: false), (lead, folded));
        Assert.NotNull(folded);
    }

    [Fact]
    public void Render_WhenListsNestUnderTheCap_ThenTheFirstThreeStillDrawAsLists()
    {
        var text = string.Concat(Enumerable.Repeat("- ", 20)) + "a";

        Assert.Equal(3, Render(CatalogueMarkdown.Render(text, formatted: true)).FindAll("ul").Count);
    }
}
