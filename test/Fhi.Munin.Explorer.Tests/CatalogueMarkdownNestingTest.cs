using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Nesting past Markdig's depth limit makes it throw; without the guard one stored value ends the circuit for
/// every reader of the page (Fhi.Metadata-7vquc.8).</summary>
public class CatalogueMarkdownNestingTest : ExplorerTestContext
{
    public static TheoryData<string, bool> TooDeep()
    {
        var data = new TheoryData<string, bool>();

        foreach (var text in new[] { DeepList, DeepQuote })
        {
            data.Add(text, false);
            data.Add(text, true);
        }

        return data;
    }

    public static TheoryData<string> Texts() => [DeepList, DeepQuote];

    private static readonly string DeepList = string.Concat(Enumerable.Repeat("- ", 64)) + "a";

    private static readonly string DeepQuote = string.Concat(Enumerable.Repeat("> ", 128)) + "a";

    [Theory]
    [MemberData(nameof(TooDeep))]
    public void Render_WhenMarkersNestPastTheParsersLimit_ThenTheTextIsDrawnAsPlainText(string text, bool formatted)
    {
        var cut = Render(CatalogueMarkdown.Render(text, formatted));

        Assert.Empty(cut.FindAll("*"));
        Assert.Equal(text, string.Concat(cut.Nodes.Select(node => node.TextContent)));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Link_WhenMarkersNestPastTheParsersLimit_ThenItIsNoLink(string text)
    {
        Assert.Null(CatalogueMarkdown.Link(text));
        Assert.Equal(text, CatalogueMarkdown.Words(text));
    }

    [Theory]
    [MemberData(nameof(TooDeep))]
    public void DrawsList_WhenMarkersNestPastTheParsersLimit_ThenItIsNo(string text, bool formatted)
    {
        Assert.False(CatalogueMarkdown.DrawsList(text, formatted));
    }

    [Theory]
    [MemberData(nameof(TooDeep))]
    public void Split_WhenMarkersNestPastTheParsersLimit_ThenTheWholeTextIsTheLead(string text, bool formatted)
    {
        Assert.Equal((text, null), DescriptionLead.Split(text, formatted));
        Assert.Empty(CatalogueMarkdown.EmphasisSpans(text));
        Assert.Empty(CatalogueMarkdown.TopBlocks(text).Blocks);
    }
}
