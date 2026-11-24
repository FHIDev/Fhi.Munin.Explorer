using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class CatalogueFoldIndentedListHeldOutTest : ExplorerTestContext
{
    private static string Intro => new string('a', 300) + ".";

    [Theory]
    [InlineData("  - ", "  - ", "ul")]
    [InlineData("   1. ", "   2. ", "ol")]
    public void Render_WhenTheFoldLandsInFrontOfAnIndentedList_ThenTheFoldedHalfDrawsSiblingItems(
        string first, string next, string tag)
    {
        var third = first.StartsWith("  -") ? "  - " : "   3. ";
        var items = new[] { first + "Første " + new string('x', 150), next + "Andre " + new string('y', 150), third + "Tredje " + new string('z', 150) };
        var text = Intro + "\n\n" + string.Join("\n", items);

        var (_, folded) = DescriptionLead.Split(text, formatted: true);

        Assert.NotNull(folded);
        var cut = Render(CatalogueMarkdown.Render(folded, formatted: true));
        Assert.Equal(3, cut.FindAll($"{tag} > li").Count);
        Assert.Empty(cut.FindAll("li ul, li ol"));
    }
}
