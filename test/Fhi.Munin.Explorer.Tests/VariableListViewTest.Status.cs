using Bunit;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A historical variable says so on its list row (Fhi.Metadata-l9l2n.190): Sidsel's warning, as text in the row
/// header so a screen reader reads it with the name, and in the search's own word.
/// </summary>
public partial class VariableListViewTest
{
    private const string StatusMark = ".munin-explorer-list-status";

    [Fact]
    public void Status_WhenTheVariableIsHistorical_ThenItsRowHeaderSaysHistorisk()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER") with { VersionStatus = "Historical" }));

        var name = cut.Find($"{OwnTable} tbody th[scope=row]");
        Assert.Equal("Historisk", name.QuerySelector(StatusMark)!.TextContent.Trim());
    }

    [Fact]
    public void Status_WhenTheVariableIsActive_ThenItsRowHasNoMark()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER") with { VersionStatus = "Active" }));

        Assert.Empty(cut.FindAll($"{OwnTable} tbody {StatusMark}"));
    }

    [Fact]
    public void Status_WhenTheRowCarriesNoStatus_ThenItHasNoMark()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        Assert.Empty(cut.FindAll($"{OwnTable} tbody {StatusMark}"));
    }

    [Fact]
    public void Status_WhenTheStatusArrivesInNorwegian_ThenTheRowStillSaysHistorisk()
    {
        // VersionStatusRule reads either language; a stored «historisk» must not fall through to no mark.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER") with { VersionStatus = "historisk" }));

        Assert.Equal("Historisk", cut.Find($"{OwnTable} tbody {StatusMark}").TextContent.Trim());
    }
}
