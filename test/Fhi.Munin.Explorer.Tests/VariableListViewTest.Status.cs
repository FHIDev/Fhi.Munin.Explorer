using Bunit;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A historical variable says so on its list row (Fhi.Metadata-l9l2n.190): text in the row header, read with the
/// row, in the search's own word and the reader's language, with or without a code.
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

    [Theory]
    [InlineData("V_BDR.ALDER", null, "Historisk")]
    [InlineData("", null, "Historisk")]
    [InlineData("V_BDR.ALDER", "en", "Historical")]
    [InlineData("", "en", "Historical")]
    public void Status_WhenAHistoricalRowIsDrawn_ThenTheMarkIsInTheReadersLanguageWithOrWithoutACode(
        string code, string? language, string expected)
    {
        // A client-built shared snapshot can carry no code; the warning must not go with it.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", code) with { VersionStatus = "Historical" }), language: language);

        Assert.Equal(expected, cut.Find($"{OwnTable} tbody th[scope=row] {StatusMark}").TextContent.Trim());
    }
}
