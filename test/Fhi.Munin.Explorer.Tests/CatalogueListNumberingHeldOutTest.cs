using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class CatalogueListNumberingHeldOutTest : ExplorerTestContext
{
    private string[] Values(string text) =>
        Render(CatalogueMarkdown.Render(text, formatted: true)).FindAll("ol > li")
            .Select(li => li.GetAttribute("value") ?? "").ToArray();

    [Fact]
    public void Render_WhenANumberedListEndsOnANumberOutOfSequence_ThenTheLastItemKeepsItsNumber()
    {
        var values = Values("1. Ja\n2. Nei\n9. Vet ikke");

        Assert.Equal(3, values.Length);
        Assert.Equal("9", values[2]);
    }

    [Fact]
    public void Render_WhenANumberedListJumpsInTheMiddle_ThenTheItemsAfterTheJumpKeepTheirNumbers()
    {
        var values = Values("1. a\n5. b\n6. c");

        Assert.Equal(3, values.Length);
        Assert.Equal("5", values[1]);
        Assert.True(values[2] is "6" or "", $"Third item carries value «{values[2]}»");
    }
}
