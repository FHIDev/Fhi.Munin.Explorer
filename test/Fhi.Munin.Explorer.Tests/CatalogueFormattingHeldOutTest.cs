using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class CatalogueFormattingHeldOutTest : ExplorerTestContext
{
    [Theory]
    [InlineData("-")]
    [InlineData("- \n- ")]
    public void Render_WhenTheValueIsOnlyAPlaceholderDash_ThenItStaysLiteralTextWithoutList(string text)
    {
        var cut = Render(CatalogueMarkdown.Render(text, formatted: true));

        Assert.Empty(cut.FindAll("ul, ol, li"));
        Assert.Contains("-", cut.Markup);
    }

    [Fact]
    public void Split_WhenTheFirstChunkIsALeadInAndATightListOfTwoSentenceItems_ThenTheCutIsNotInsideAnItem()
    {
        var items = Enumerable.Range(1, 8)
            .Select(i => $"- Punkt {i} forklarer en ting om registeret. Den andre setningen utdyper punkt {i} med flere ord.");
        var text = "Kilden inneholder følgende opplysninger:\n" + string.Join("\n", items);

        var (lead, folded) = DescriptionLead.Split(text, formatted: true);

        Assert.NotNull(folded);
        var lastLine = lead.Split('\n')[^1];
        Assert.True(
            !lastLine.StartsWith("- ") || lastLine.EndsWith("ord."),
            $"The lead ends inside a list item: «{lastLine}»");
        Assert.StartsWith("- ", folded.Split('\n')[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenListsNestFiveLevelsDeep_ThenAtMostThreeUlElementsAreDrawn()
    {
        var cut = Render(CatalogueMarkdown.Render("- 1\n  - 2\n    - 3\n      - 4\n        - 5", formatted: true));

        Assert.True(cut.FindAll("ul").Count <= 3, $"Drew {cut.FindAll("ul").Count} ul elements");
    }
}
