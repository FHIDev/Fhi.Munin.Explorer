using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// Same-named rows on a page carry their code under the name; a unique name is drawn as before (Fhi.Metadata-l9l2n.161).
public class SharedNameRowsTest : ExplorerTestContext
{
    private const string Shared = "Tiltak kodet via ICD-10";

    // The same rows for every search, unless `bySearch` names that search.
    private sealed class FakeClient(VariableSummary[] rows, Dictionary<string, VariableSummary[]>? bySearch = null)
        : EmptyMuninExplorerClient
    {
        public FakeClient(params VariableSummary[] rows) : this(rows, null) { }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            var items = search is not null && bySearch?.TryGetValue(search, out var found) == true ? found : rows;
            return Task.FromResult(new Page<VariableSummary>
            {
                Items = items, TotalCount = items.Length, PageNumber = 1, Size = 25, TotalPages = 1,
            });
        }
    }

    private static VariableSummary Variable(string name, string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        PreferredTerm = name,
        KildeName = "Parkinsonregisteret",
        DatasamlingName = "Oppfølging",
    };

    private IRenderedComponent<VariableSearch> RenderTwoSharedOneUnique()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(
            Variable(Shared, "V_PARKINSON.F7.NPR_PROSEDYRER_1"),
            Variable("Diagnosedato", "V_PARKINSON.F1.DIAGNOSEDATO"),
            Variable(Shared, "V_PARKINSON.F7.NPR_PROSEDYRER_2")));

        return Render<VariableSearch>();
    }

    private static IElement Toggle(IRenderedComponent<VariableSearch> cut, int row) =>
        cut.FindAll("ul.munin-explorer-data-list > li")[row].QuerySelector("button.munin-explorer-dataitem-main__name")!;

    [Fact]
    public void Rows_WhenTwoShareAName_ThenEachShowsItsCodeUnderTheName()
    {
        var cut = RenderTwoSharedOneUnique();

        var first = Toggle(cut, 0);
        var name = first.QuerySelector(".munin-explorer-dataitem-main__column__text")!;
        var code = first.QuerySelector(".munin-explorer-list-code")!;

        Assert.Equal(Shared, name.TextContent);
        Assert.Equal("V_PARKINSON.F7.NPR_PROSEDYRER_1", code.TextContent);
        Assert.Same(name.ParentElement, code.ParentElement);
        Assert.Equal("SPAN", code.ParentElement!.TagName);
        Assert.Null(code.GetAttribute("lang"));
        Assert.Equal("V_PARKINSON.F7.NPR_PROSEDYRER_2",
                     Toggle(cut, 2).QuerySelector(".munin-explorer-list-code")!.TextContent);
    }

    [Fact]
    public void Rows_WhenTwoShareAName_ThenTheCodeIsInEachRowsAccessibleName()
    {
        var cut = RenderTwoSharedOneUnique();

        Assert.Equal($"Vis detaljer for {Shared}, V_PARKINSON.F7.NPR_PROSEDYRER_1", AccessibleName.Of(Toggle(cut, 0)));
        Assert.Equal($"Vis detaljer for {Shared}, V_PARKINSON.F7.NPR_PROSEDYRER_2", AccessibleName.Of(Toggle(cut, 2)));
    }

    [Fact]
    public void Rows_WhenTwoShareAName_ThenTheirSaveButtonsNameTheCodeAndAUniqueOneDoesNot()
    {
        Services.AddScoped<VariableListState>();
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(
            Variable(Shared, "V_PARKINSON.F7.NPR_PROSEDYRER_1"),
            Variable("Diagnosedato", "V_PARKINSON.F1.DIAGNOSEDATO"),
            Variable(Shared, "V_PARKINSON.F7.NPR_PROSEDYRER_2")));
        var cut = Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, true));

        var saves = cut.FindAll(".munin-explorer-dataitem-main__save button");

        Assert.Equal($"Lagre i liste {Shared} V_PARKINSON.F7.NPR_PROSEDYRER_1", AccessibleName.Of(saves[0]));
        Assert.Equal("Lagre i liste Diagnosedato", AccessibleName.Of(saves[1]));

        Toggle(cut, 2).Click();
        var panelSave = cut.Find(".munin-explorer-detail button[aria-pressed]");

        Assert.Equal($"Lagre i liste {Shared} V_PARKINSON.F7.NPR_PROSEDYRER_2", AccessibleName.Of(panelSave));
    }

    [Fact]
    public void Rows_WhenASharedNameHasNoCode_ThenThatRowDrawsNoEmptyCode()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(
            Variable(Shared, "V_PARKINSON.F7.NPR_PROSEDYRER_1"),
            Variable(Shared, " ")));
        var cut = Render<VariableSearch>();

        Assert.Null(Toggle(cut, 1).QuerySelector(".munin-explorer-list-code"));
        Assert.Equal($"Vis detaljer for {Shared}", AccessibleName.Of(Toggle(cut, 1)));
    }

    [Fact]
    public void Rows_WhenANameIsUnique_ThenTheRowIsDrawnAsBefore()
    {
        var cut = RenderTwoSharedOneUnique();

        var unique = Toggle(cut, 1);

        Assert.Null(unique.QuerySelector(".munin-explorer-list-code"));
        Assert.Equal("munin-explorer-dataitem-main__column__text", unique.Children[1].ClassName);
        Assert.Equal("Vis detaljer for Diagnosedato", AccessibleName.Of(unique));
    }

    [Fact]
    public void Rows_WhenTheCodeColumnIsOn_ThenTheCodeIsNotDrawnTwice()
    {
        var cut = RenderTwoSharedOneUnique();

        var box = cut.FindAll("[aria-labelledby^='munin-explorer-columns-toggle-'] input[type=checkbox]")
                     .Single(b => b.ParentElement!.TextContent.Trim() == "Kode");
        box.Change(true);

        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list .munin-explorer-list-code"));
        Assert.Equal($"Vis detaljer for {Shared}", AccessibleName.Of(Toggle(cut, 0)));
    }

    [Fact]
    public void Rows_WhenOneVariableIsInTwoDatasamlinger_ThenNoCodeIsDrawn()
    {
        var id = Guid.NewGuid();
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(
            Variable(Shared, "V_PARKINSON.F7.NPR_PROSEDYRER_1") with { Id = id, RowKey = $"{id}:a", DatasamlingName = "Oppfølging" },
            Variable(Shared, "V_PARKINSON.F7.NPR_PROSEDYRER_1") with { Id = id, RowKey = $"{id}:b", DatasamlingName = "Inklusjon" }));
        var cut = Render<VariableSearch>();

        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list .munin-explorer-list-code"));
        Assert.Equal($"Vis detaljer for {Shared}", AccessibleName.Of(Toggle(cut, 1)));
    }

    [Fact]
    public void Rows_WhenANewSearchHasNoSharedName_ThenTheCodesGo()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(
            [Variable(Shared, "V_PARKINSON.F7.NPR_PROSEDYRER_1"), Variable(Shared, "V_PARKINSON.F7.NPR_PROSEDYRER_2")],
            new() { ["dato"] = [Variable("Diagnosedato", "V_PARKINSON.F1.DIAGNOSEDATO"), Variable("Dødsdato", "V_PARKINSON.F9.DODSDATO")] }));
        var cut = Render<VariableSearch>();
        Assert.Equal(2, cut.FindAll("ul.munin-explorer-data-list .munin-explorer-list-code").Count);

        cut.Find(".searchbox__freetext").Change("dato");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Vis detaljer for Diagnosedato", AccessibleName.Of(Toggle(cut, 0))));
        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list .munin-explorer-list-code"));
    }

    [Fact]
    public void Rows_WhenOnlyTheEnglishNamesAreShared_ThenAnEnglishReaderSeesTheCodesAndANorwegianOneDoesNot()
    {
        var rows = new[]
        {
            Variable("Tiltak kodet via ICD-10", "V_A") with { PreferredTermEn = "Procedure code" },
            Variable("Tiltak kodet via NCSP", "V_B") with { PreferredTermEn = "Procedure code" },
        };
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(rows));
        var cut = Render<VariableSearch>(p => p.Add(c => c.Language, "en"));

        Assert.Equal("V_A", Toggle(cut, 0).QuerySelector(".munin-explorer-list-code")!.TextContent);

        cut.Render(b => b.Add(c => c.Language, "no"));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("ul.munin-explorer-data-list .munin-explorer-list-code")));
    }

    [Fact]
    public void Rows_WhenASharedRowIsOpen_ThenItsCollapseLabelNamesTheCode()
    {
        var cut = RenderTwoSharedOneUnique();

        Toggle(cut, 0).Click();

        Assert.Equal($"Skjul detaljer for {Shared}, V_PARKINSON.F7.NPR_PROSEDYRER_1", AccessibleName.Of(Toggle(cut, 0)));
    }

    [Fact]
    public void Rows_WhenTwoBlankNamesHaveCodes_ThenEachShowsItsCodeAndIsNamedByItOnce()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Variable("", "V_A"), Variable("", "V_B")));
        var cut = Render<VariableSearch>();

        Assert.Equal("V_A", Toggle(cut, 0).QuerySelector(".munin-explorer-list-code")!.TextContent);
        Assert.Equal("Vis detaljer for V_A", AccessibleName.Of(Toggle(cut, 0)));
    }
}
