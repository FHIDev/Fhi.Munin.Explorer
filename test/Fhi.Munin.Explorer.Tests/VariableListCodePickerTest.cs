using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The code picker as an opened list row offers it, under «Ønskede data» (Fhi.Metadata-emsc0.3.7).</summary>
public class VariableListCodePickerTest : ExplorerTestContext
{
    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Variable = new("bc8a6515-af36-44d0-aa10-b498813327b4");
    private static readonly Guid ItemId = new("83ced574-6d32-447e-ad65-c54c63327476");

    private static readonly KodeverkLink Sivilstand =
        new() { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", DisplayName = "Sivilstand", HasCodeValues = true };

    private sealed class ListClient(VariableListItem item, params KodeverkLink[] links) : EmptyMuninExplorerClient
    {
        public List<(Guid ItemId, string? FreeText, IReadOnlyList<DesiredCodeChoice> Choices)> Writes { get; } = [];
        public TaskCompletionSource<DesiredDataResult>? Pending { get; set; }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = ListId, Name = "Kreft", VariableCount = 1 }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [item],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical, Guid? datasamlingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(new VariableDetail { Id = id, PreferredTerm = "Sivilstand", KodeverkLinks = links });

        public override Task<KodeverkCodes?> GetKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, CancellationToken cancellationToken = default) =>
            Task.FromResult<KodeverkCodes?>(new KodeverkCodes
            {
                Codes = [new KodeverkCode { Value = "1", Name = "Ugift" }, new KodeverkCode { Value = "2", Name = "Gift" }],
            });

        public override Task<DesiredDataResult> SetMyListItemDesiredCodesAsync(
            Guid id, Guid itemId, string? freeText, IReadOnlyList<DesiredCodeChoice> choices,
            CancellationToken cancellationToken = default)
        {
            Writes.Add((itemId, freeText, choices));
            return Pending?.Task ?? Task.FromResult(new DesiredDataResult(DesiredDataOutcome.Saved));
        }
    }

    private static VariableListItem Item(Guid? itemId = null) => new()
    {
        ItemId = itemId ?? ItemId,
        VariableId = Variable,
        VariableName = "Sivilstand",
        DatasamlingId = new Guid("0c027ce6-2994-45bf-99ec-facad3d1703e"),
        DatasamlingName = "Lungekreft",
        DesiredDataFreeText = "bare siste registrerte",
    };

    private IRenderedComponent<VariableListView> OpenedRow(ListClient client)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForElement("table.munin-explorer-data-list tbody tr.munin-explorer-data-list__item th[scope=row] button")
            .Click(new MouseEventArgs { Detail = 1 });
        cut.WaitForElement("[role=tablist]");
        return cut;
    }

    private static IElement? OpenButton(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("button[aria-controls$='-picker']").SingleOrDefault();

    [Fact]
    public void Picker_WhenTheRowsVariableHasACodeList_ThenItOpensSavesTheCodesWithTheFreeTextAndTheRowSaysSo()
    {
        var client = new ListClient(Item(), Sivilstand);
        var cut = OpenedRow(client);

        var open = OpenButton(cut)!;
        Assert.Equal("false", open.GetAttribute("aria-expanded"));
        open.Click(new MouseEventArgs { Detail = 1 });
        cut.WaitForAssertion(() => Assert.Equal("true", OpenButton(cut)!.GetAttribute("aria-expanded")));

        cut.WaitForAssertion(() => Assert.Contains("Ugift", cut.Markup));
        cut.FindAll("label").First(l => l.TextContent.Contains("Ugift", StringComparison.Ordinal))
            .QuerySelector("input[type=checkbox]")!.Change(true);
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Lagre").Click();

        var write = Assert.Single(client.Writes);
        Assert.Equal((ItemId, "bare siste registrerte"), (write.ItemId, write.FreeText));
        Assert.Equal(new DesiredCodeChoice("Kildekodeverk", "2336", DesiredCodeKinds.Code, "1"), Assert.Single(write.Choices));
        cut.WaitForAssertion(() => Assert.Equal("false", OpenButton(cut)!.GetAttribute("aria-expanded")));
        Assert.Contains(cut.FindAll("[role=status]"), status => status.TextContent.Trim() == "Lagret: 1 av 200 valg");
    }

    [Fact]
    public void Picker_WhenTheVariableHasNoCodeListWithValues_ThenThereIsNone()
    {
        var cut = OpenedRow(new ListClient(Item(), Sivilstand with { HasCodeValues = false }));

        Assert.Null(OpenButton(cut));
    }

    [Fact]
    public void TogglePicker_WhenAStandingGestureLandsOnTheButton_ThenThePickerStaysShut()
    {
        var cut = OpenedRow(new ListClient(Item(), Sivilstand));

        OpenButton(cut)!.Click(new MouseEventArgs { Detail = 2 });

        Assert.Equal("false", OpenButton(cut)!.GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task SaveCodes_WhenAnOlderPickersSaveLandsUnderANewerOne_ThenTheNewerOneStaysOpenAndNothingIsSaidSaved()
    {
        var client = new ListClient(Item(), Sivilstand) { Pending = new TaskCompletionSource<DesiredDataResult>(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = OpenedRow(client);
        OpenButton(cut)!.Click(new MouseEventArgs { Detail = 1 });
        cut.WaitForAssertion(() => Assert.Contains("Ugift", cut.Markup));
        cut.FindAll("label").First(l => l.TextContent.Contains("Ugift", StringComparison.Ordinal)).QuerySelector("input[type=checkbox]")!.Change(true);
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Lagre").Click();

        // Closed and opened again while the save is out: a newer picker for the same row.
        OpenButton(cut)!.Click(new MouseEventArgs { Detail = 1 });
        OpenButton(cut)!.Click(new MouseEventArgs { Detail = 1 });
        client.Pending.SetResult(new DesiredDataResult(DesiredDataOutcome.Saved));

        // Settled first: the late answer and the render it causes have run before anything is asserted.
        await Task.Delay(50);
        await cut.InvokeAsync(() => { });
        Assert.Equal("true", OpenButton(cut)!.GetAttribute("aria-expanded"));
        Assert.DoesNotContain(cut.FindAll("[role=status]"), status => status.TextContent.Contains("Lagret", StringComparison.Ordinal));
    }

    [Fact]
    public void Row_WhenTheItemHasChosenCodes_ThenTheyAreSummarisedUnderItsFreeText()
    {
        var item = Item() with { DesiredDataCodes = [new DesiredCodeView { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", KodeverkName = "Sivilstand", Kind = "kode", Code = "2", Name = "Gift" }] };
        Services.AddSingleton<IMuninExplorerClient>(new ListClient(item, Sivilstand));
        Services.AddScoped<VariableListState>();
        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));

        var cell = cut.WaitForElement("td.munin-explorer-dataitem-main__desiredData");
        Assert.NotNull(cell.QuerySelector("textarea"));
        Assert.Contains("Sivilstand: 2 Gift", System.Text.RegularExpressions.Regex.Replace(cell.TextContent, @"\s+", " "));
    }
}
