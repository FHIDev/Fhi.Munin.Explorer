using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The saved list's columns as Inge decided them (Fhi.Metadata-b2w2z): six by default so the table fits
/// 1280, the code under the name, the rest behind the column picker, and a visible «Valg» heading.
/// </summary>
public partial class VariableListViewTest
{
    private const string OwnTable = "table.munin-explorer-data-list";

    private static IReadOnlyList<string> HeaderTexts(IRenderedComponent<VariableListView> cut) =>
        [.. cut.FindAll($"{OwnTable} thead th").Select(th => th.TextContent.Trim())];

    private static IReadOnlyList<IElement> ListColumnToggles(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll(".dropdown-choicepicker__item input[type=checkbox]");

    private static string ListColumnName(IElement toggle) =>
        toggle.ParentElement!.QuerySelector(".form-control__label")!.TextContent.Trim();

    private static void ToggleListColumn(IRenderedComponent<VariableListView> cut, string label)
    {
        var box = ListColumnToggles(cut).Single(b => ListColumnName(b) == label);
        box.Change(!box.HasAttribute("checked"));
    }

    /// <summary>Ticks every column the picker has unticked, for a test about the cells behind it.</summary>
    private static void ShowEveryColumn(IRenderedComponent<VariableListView> cut)
    {
        foreach (var label in ListColumnToggles(cut).Where(b => !b.HasAttribute("checked")).Select(ListColumnName).ToList())
        {
            ToggleListColumn(cut, label);
        }
    }

    [Fact]
    public void Columns_WhenFirstDrawn_ThenTheTableShowsTheSixThatFit1280()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        Assert.Equal(["Navn", "Kilde", "Datasamling", "Variabelgruppe", "Ønskede data", "Valg"], HeaderTexts(cut));
    }

    [Fact]
    public void Columns_WhenFirstDrawn_ThenEveryRowHasACellUnderEveryHeading()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var row = cut.FindAll($"{OwnTable} tbody tr.munin-explorer-data-list__item")[0];
        Assert.Equal(HeaderTexts(cut).Count, row.Children.Length);
    }

    [Fact]
    public void Columns_WhenARowIsDrawn_ThenItsCodeStandsUnderTheName()
    {
        // The code column was the widest; under the name it still reads and copies whole.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var name = cut.Find($"{OwnTable} tbody th[scope=row]");
        Assert.Equal("V_BDR.ALDER", name.QuerySelector(".munin-explorer-list-code")!.TextContent.Trim());
        Assert.Empty(cut.FindAll($"{OwnTable} thead th.munin-explorer-dataitem-header__code"));
    }

    [Fact]
    public void Columns_WhenTheRowHasNoCode_ThenNothingStandsUnderTheName()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "")));

        Assert.Empty(cut.FindAll($"{OwnTable} tbody .munin-explorer-list-code"));
    }

    [Fact]
    public void ColumnPicker_WhenDrawn_ThenItOffersTheOptionalColumnsWithTheHiddenOnesUnticked()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var offered = ListColumnToggles(cut).ToDictionary(ListColumnName, b => b.HasAttribute("checked"));
        Assert.Equal(
            new Dictionary<string, bool>
            {
                ["Kilde"] = true,
                ["Datasamling"] = true,
                ["Variabelgruppe"] = true,
                ["Datatype"] = false,
                ["Dataperiode"] = false,
                ["Kodeverk"] = false,
                ["Statistikk"] = false,
            },
            offered);
    }

    [Fact]
    public void Columns_WhenAHiddenColumnIsTicked_ThenItsHeadingAndCellsAppearInPlace()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        ToggleListColumn(cut, "Datatype");

        Assert.Equal(["Navn", "Kilde", "Datasamling", "Variabelgruppe", "Datatype", "Ønskede data", "Valg"], HeaderTexts(cut));
        var row = cut.FindAll($"{OwnTable} tbody tr.munin-explorer-data-list__item")[0];
        Assert.Equal(7, row.Children.Length);
    }

    [Fact]
    public void Columns_WhenAShownColumnIsUnticked_ThenItLeaves()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        ToggleListColumn(cut, "Kilde");

        Assert.Equal(["Navn", "Datasamling", "Variabelgruppe", "Ønskede data", "Valg"], HeaderTexts(cut));
    }

    [Fact]
    public void Columns_WhenARowIsOpened_ThenItsPanelSpansTheColumnsOnScreen()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        ToggleListColumn(cut, "Dataperiode");

        cut.Find($"{OwnTable} tbody th[scope=row] button").Click();

        cut.WaitForAssertion(() => Assert.Equal("7", cut.Find($"{OwnTable} td[colspan]").GetAttribute("colspan")));
    }

    [Fact]
    public void RemoveColumn_WhenDrawn_ThenItIsHeadedValgOnScreenAndInTheCards()
    {
        // The old helsedata page heads it «Valg», and Inge kept that; the cards label the button the same.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var heading = cut.FindAll($"{OwnTable} thead th")[^1];
        Assert.Equal("Valg", heading.TextContent.Trim());
        Assert.Empty(heading.QuerySelectorAll(".screenreader-only"));
        Assert.Equal("Valg", cut.Find($"{OwnTable} tbody td button[id^='munin-explorer-list-remove-']").ParentElement!.GetAttribute("data-label"));
    }

    [Fact]
    public void DesiredData_WhenDrawn_ThenItIsATextareaOneLineHigh()
    {
        // A text field cut a long note off; a textarea wraps, and Stiler grows it to four lines.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var field = cut.Find($"{OwnTable} td.munin-explorer-dataitem-main__desiredData textarea");
        Assert.Equal("1", field.GetAttribute("rows"));
        Assert.Contains("textarea__field", field.ClassList);
        Assert.Empty(cut.FindAll($"{OwnTable} td.munin-explorer-dataitem-main__desiredData input"));
    }
}
