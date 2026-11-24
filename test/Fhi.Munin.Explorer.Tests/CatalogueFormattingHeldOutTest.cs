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
    public void Render_WhenListsNestFiveLevelsDeep_ThenAtMostThreeUlElementsAreDrawn()
    {
        var cut = Render(CatalogueMarkdown.Render("- 1\n  - 2\n    - 3\n      - 4\n        - 5", formatted: true));

        Assert.True(cut.FindAll("ul").Count <= 3, $"Drew {cut.FindAll("ul").Count} ul elements");
    }
}
