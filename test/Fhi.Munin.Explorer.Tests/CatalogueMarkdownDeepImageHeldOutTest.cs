using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class CatalogueMarkdownDeepImageHeldOutTest : ExplorerTestContext
{
    // Deeply nested image openers pass Markdig; a recursive walk overflows the stack and kills the host.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Render_WhenTheTextIsManyNestedImageOpeners_ThenItRendersAsPlainText(bool formatted)
    {
        var text = string.Concat(Enumerable.Repeat("![", 9990)) + "a";

        var cut = Render(CatalogueMarkdown.Render(text, formatted));

        Assert.Equal(text, cut.Markup.Replace("&#xA;", "\n"));
        Assert.Empty(cut.FindAll("*"));
    }
}
