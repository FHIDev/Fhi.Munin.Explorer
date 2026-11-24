using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class CatalogueIntrawordEmphasisHeldOutTest : ExplorerTestContext
{
    [Theory]
    [InlineData("Dose = 2*3*4 mg.")]
    [InlineData("Verdi A(*) og verdi B(*).")]
    public void Render_WhenAStarSitsBetweenWordCharacters_ThenTheTextStaysLiteral(string text)
    {
        var cut = Render(CatalogueMarkdown.Render(text, formatted: true));

        Assert.Empty(cut.FindAll("em"));
        Assert.Equal(text, cut.Markup);
    }

    [Fact]
    public void Render_WhenAWordIsWrappedInStars_ThenItIsEmphasised()
    {
        var cut = Render(CatalogueMarkdown.Render("Dette er *viktig* her.", formatted: true));

        Assert.Equal("viktig", Assert.Single(cut.FindAll("em")).TextContent);
    }
}
