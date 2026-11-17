using Bunit;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The row header's text keeps the code a word of its own (Fhi.Metadata-l9l2n.190): Razor emits no whitespace
/// between the name and the code span, and a host that draws the code inline would read «diagnoseV_BDR.ALDER».
/// </summary>
public partial class VariableListViewTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("Historical")]
    public void UnderName_WhenARowHasACode_ThenWhitespaceSeparatesItFromTheName(string? status)
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER") with { VersionStatus = status }));

        var text = cut.Find($"{OwnTable} tbody th[scope=row]").TextContent;
        var at = text.IndexOf("V_BDR.ALDER", StringComparison.Ordinal);
        Assert.True(at > 0 && char.IsWhiteSpace(text[at - 1]), $"no whitespace before the code in «{text.Trim()}»");
    }
}
