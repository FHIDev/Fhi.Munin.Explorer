using Bunit;

namespace Fhi.Munin.Explorer.Tests;

// Held out: deleting this lets the row header read «V_BDR.ALDERHistorisk» to a screen reader (Fhi.Metadata-l9l2n.190).
public partial class VariableListViewTest
{
    [Theory]
    [InlineData("V_BDR.ALDER")]
    [InlineData("")]
    public void Status_WhenAHistoricalRowIsDrawn_ThenWhitespaceSeparatesTheMarkFromWhatPrecedesIt(string code)
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", code) with { VersionStatus = "Historical" }));

        var text = cut.Find($"{OwnTable} tbody th[scope=row]").TextContent;

        var at = text.IndexOf("Historisk", StringComparison.Ordinal);
        Assert.True(at > 0, $"no mark in «{text}»");
        Assert.True(char.IsWhiteSpace(text[at - 1]), $"no whitespace before the mark in «{text}»");
    }
}
