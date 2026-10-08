using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A saved list keyed by (variable, datasamling): one row per item, notes and "Ønskede data" by
/// item id, and the picker that resolves an item saved before lists named a datasamling
/// (Fhi.Metadata-d07al.1, acceptance A8, the RCL half).
/// </summary>
public class ListItemsPerDatasamlingTest : ExplorerTestContext
{
    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Kreft = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Variable = new("bc8a6515-af36-44d0-aa10-b498813327b4");
    private static readonly Guid Lungekreft = new("0c027ce6-2994-45bf-99ec-facad3d1703e");
    private static readonly Guid Livmorhals = new("4411873c-0367-4334-a7cf-8e763c8e3490");
    private static readonly Guid Barnekreft = new("7d1e0c55-3a2b-4f4e-9b8a-1f0e2d3c4b5a");

    private static VariableListItem Item(Guid? datasamling, string? datasamlingName) => new()
    {
        ItemId = Guid.NewGuid(),
        VariableId = Variable,
        VariableName = "Operasjonsdato for primærtumor",
        VariableCode = "V_KREG.S_DATOOPRPRIMAR",
        KildeId = Kreft,
        KildeName = "Kreftregisteret",
        DatasamlingId = datasamling,
        DatasamlingName = datasamlingName,
    };

    private static VariableListItem Unresolved() => Item(null, null) with
    {
        DesiredDataFreeText = "C34",
        Notes = "Spør om 2012",
        CandidateDatasamlinger =
        [
            new DatasamlingCandidate { Id = Lungekreft, Name = "Lungekreft" },
            new DatasamlingCandidate { Id = Livmorhals, Name = "Livmorhals" },
            new DatasamlingCandidate { Id = Barnekreft, Name = "Barnekreft" },
        ],
    };

    private sealed class ItemClient(params VariableListItem[] all) : EmptyMuninExplorerClient
    {
        public List<VariableListItem> Items { get; } = [.. all];
        public List<string> Calls { get; } = [];
        public List<(Guid ItemId, string? Text)> NotesByItem { get; } = [];
        public List<(Guid ItemId, string? Text)> DesiredByItem { get; } = [];
        public int VariableRouteWrites { get; private set; }
        public bool FailAdd { get; init; }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = ListId, Name = "Kreft", VariableCount = Items.Count }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. Items],
                TotalCount = Items.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<bool> AddItemsToMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
            Calls.Add($"add {string.Join(",", items.Select(i => i.DatasamlingId))}");

            if (FailAdd)
            {
                throw new HttpRequestException("nede");
            }

            Items.AddRange(items.Select(i => Item(i.DatasamlingId, null)));
            return Task.FromResult(true);
        }

        public override Task<bool> RemoveItemsFromMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
            Calls.Add($"remove {string.Join(",", items.Select(i => i.DatasamlingId?.ToString() ?? "none"))}");
            Items.RemoveAll(i => items.Contains(VariableDatasamlingKey.Of(i)));
            return Task.FromResult(true);
        }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            Calls.Add("remove by variable");
            return Task.FromResult(true);
        }

        public override Task<DesiredDataResult> SetMyListItemNotesAsync(
            Guid id, Guid itemId, string? text, CancellationToken cancellationToken = default)
        {
            Calls.Add("notes");
            NotesByItem.Add((itemId, text));
            Items.FindAll(i => i.ItemId == itemId).ForEach(i => Items[Items.IndexOf(i)] = i with { Notes = text });
            return Task.FromResult(new DesiredDataResult(DesiredDataOutcome.Saved));
        }

        public override Task<DesiredDataResult> SetMyListItemDesiredDataAsync(
            Guid id, Guid itemId, string? freeText, CancellationToken cancellationToken = default)
        {
            Calls.Add("desired");
            DesiredByItem.Add((itemId, freeText));
            Items.FindAll(i => i.ItemId == itemId).ForEach(i => Items[Items.IndexOf(i)] = i with { DesiredDataFreeText = freeText });
            return Task.FromResult(new DesiredDataResult(DesiredDataOutcome.Saved));
        }

        public List<Guid?> DetailsFrom { get; } = [];

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical, Guid? datasamlingId, CancellationToken cancellationToken = default)
        {
            DetailsFrom.Add(datasamlingId);
            return Task.FromResult<VariableDetail?>(new VariableDetail { Id = id, PreferredTerm = "Operasjonsdato for primærtumor" });
        }

        public override Task<DesiredDataResult> SetMyListNotesAsync(
            Guid id, Guid variableId, string? text, CancellationToken cancellationToken = default)
        {
            VariableRouteWrites++;
            return Task.FromResult(new DesiredDataResult(DesiredDataOutcome.Saved));
        }

        public override Task<DesiredDataResult> SetMyListDesiredDataAsync(
            Guid id, Guid variableId, string? freeText, CancellationToken cancellationToken = default)
        {
            VariableRouteWrites++;
            return Task.FromResult(new DesiredDataResult(DesiredDataOutcome.Saved));
        }
    }

    private IRenderedComponent<VariableListView> RenderView(ItemClient client)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        return Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
    }

    private static IReadOnlyList<IElement> Rows(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("table.munin-explorer-data-list tbody tr.munin-explorer-data-list__item");

    private static string DatasamlingCell(IElement row) =>
        row.QuerySelector(".munin-explorer-dataitem-main__dataCollection .munin-explorer-dataitem-main__column__text")!.TextContent;

    private static void OpenRow(IRenderedComponent<VariableListView> cut, int row) =>
        Rows(cut)[row].QuerySelector("th[scope=row] button")!.Click(new MouseEventArgs { Detail = 1 });

    private static IElement Picker(IRenderedComponent<VariableListView> cut) =>
        cut.Find("[id^='munin-explorer-list-panel-'][role=region] fieldset");

    private static void Tick(IRenderedComponent<VariableListView> cut, string name) =>
        Picker(cut).QuerySelectorAll("label.form-control")
            .Single(l => l.TextContent.Trim() == name)
            .QuerySelector("input[type=checkbox]")!
            .Change(true);

    private static IElement SaveChoice(IRenderedComponent<VariableListView> cut) =>
        Picker(cut).QuerySelector("button")!;

    [Fact]
    public void Render_WhenOneVariableIsInTheListFromTwoDatasamlinger_ThenEachItemIsARowOfItsOwn()
    {
        var cut = RenderView(new ItemClient(Item(Lungekreft, "Lungekreft"), Item(Livmorhals, "Livmorhals")));

        cut.WaitForAssertion(() => Assert.Equal(["Lungekreft", "Livmorhals"], Rows(cut).Select(DatasamlingCell)));
        Assert.Equal(2, cut.FindAll("th[scope=row] [id^='munin-explorer-list-name-']").Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void Remove_WhenOneOfTheTwoItemsIsRemoved_ThenOnlyItsPairIsSent()
    {
        var client = new ItemClient(Item(Lungekreft, "Lungekreft"), Item(Livmorhals, "Livmorhals"));
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(2, Rows(cut).Count));

        Rows(cut)[1].QuerySelector("[id^='munin-explorer-list-remove-']")!.Click();

        cut.WaitForAssertion(() => Assert.Equal([$"remove {Livmorhals}"], client.Calls));
        cut.WaitForAssertion(() => Assert.Equal(["Lungekreft"], Rows(cut).Select(DatasamlingCell)));
    }

    [Fact]
    public void DesiredData_WhenTheItemHasAnId_ThenItIsWrittenByItemAndNotByVariable()
    {
        // The variable's own route answers 409 once the variable is in the list twice.
        var lungekreft = Item(Lungekreft, "Lungekreft");
        var client = new ItemClient(lungekreft, Item(Livmorhals, "Livmorhals"));
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(2, Rows(cut).Count));

        Rows(cut)[0].QuerySelector("td.munin-explorer-dataitem-main__desiredData textarea")!.Change("C34.1");

        cut.WaitForAssertion(() => Assert.Equal([(lungekreft.ItemId!.Value, (string?)"C34.1")], client.DesiredByItem));
        Assert.Equal(0, client.VariableRouteWrites);
    }

    [Fact]
    public void Notes_WhenTheItemHasAnId_ThenTheyAreWrittenByItem()
    {
        var livmorhals = Item(Livmorhals, "Livmorhals");
        var client = new ItemClient(Item(Lungekreft, "Lungekreft"), livmorhals);
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(2, Rows(cut).Count));

        OpenRow(cut, 1);
        cut.WaitForElement("[role=tablist]");
        Assert.Equal([(Guid?)Livmorhals], client.DetailsFrom);
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Mine notater").Click();
        cut.Find("textarea[id^='munin-explorer-list-panel-notes-']").Change("Bare 2010");

        cut.WaitForAssertion(() => Assert.Equal([(livmorhals.ItemId!.Value, (string?)"Bare 2010")], client.NotesByItem));
        Assert.Equal(0, client.VariableRouteWrites);
    }

    [Fact]
    public void Unresolved_WhenTheListIsDrawn_ThenItsDatasamlingReadsNotChosenAndItsPanelOffersTheCandidates()
    {
        var cut = RenderView(new ItemClient(Unresolved()));
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));

        Assert.Equal("Datasamling ikke valgt", DatasamlingCell(Rows(cut)[0]));

        OpenRow(cut, 0);

        Assert.Equal("Velg datasamling", Picker(cut).QuerySelector("legend")!.TextContent);
        Assert.Equal(
            ["Lungekreft", "Livmorhals", "Barnekreft"],
            Picker(cut).QuerySelectorAll("label.form-control").Select(l => l.TextContent.Trim()));
        Assert.Equal(Picker(cut).GetAttribute("aria-describedby"), Picker(cut).QuerySelector("p.caption")!.Id);
    }

    [Fact]
    public void Unresolved_WhenTwoAreChosen_ThenTheyAreAddedTheNotesCopiedToBothAndOnlyThenTheOldItemRemoved()
    {
        var client = new ItemClient(Unresolved());
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        OpenRow(cut, 0);

        Tick(cut, "Lungekreft");
        Tick(cut, "Barnekreft");
        SaveChoice(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal(
            [$"add {Lungekreft},{Barnekreft}", "desired", "notes", "desired", "notes", "remove none"],
            client.Calls));
        Assert.All(client.DesiredByItem, write => Assert.Equal("C34", write.Text));
        Assert.All(client.NotesByItem, write => Assert.Equal("Spør om 2012", write.Text));
        Assert.Equal(2, client.NotesByItem.Select(w => w.ItemId).Distinct().Count());
        cut.WaitForAssertion(() => Assert.Equal(2, Rows(cut).Count));
    }

    [Fact]
    public void Unresolved_WhenItsDesiredDataWasEditedFirst_ThenTheEditIsWhatIsCopied()
    {
        // The words come from what the API holds at the choice, not from the page as first read.
        var client = new ItemClient(Unresolved());
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));

        Rows(cut)[0].QuerySelector("td.munin-explorer-dataitem-main__desiredData textarea")!.Change("C34.9");
        cut.WaitForAssertion(() => Assert.Single(client.DesiredByItem));
        OpenRow(cut, 0);
        Tick(cut, "Livmorhals");
        SaveChoice(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal([$"desired", $"add {Livmorhals}", "desired", "notes", "remove none"], client.Calls));
        Assert.Equal("C34.9", client.DesiredByItem[^1].Text);
    }

    [Fact]
    public void Unresolved_WhenNoneIsChosen_ThenNothingIsSentAndTheReaderIsTold()
    {
        var client = new ItemClient(Unresolved());
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        OpenRow(cut, 0);

        SaveChoice(cut).Click();

        Assert.Empty(client.Calls);
        Assert.Equal("Velg minst én datasamling.", Picker(cut).QuerySelector("[role=alert]")!.TextContent);
    }

    [Fact]
    public void Unresolved_WhenTheAddFails_ThenTheOldItemStaysWithItsWords()
    {
        // Nothing is removed until every copy landed, so a failure cannot lose the reader's notes.
        var client = new ItemClient(Unresolved()) { FailAdd = true };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        OpenRow(cut, 0);

        Tick(cut, "Livmorhals");
        SaveChoice(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "Kunne ikke lagre valget nå. Prøv igjen om litt.", Picker(cut).QuerySelector("[role=alert]")!.TextContent));
        Assert.Equal([$"add {Livmorhals}"], client.Calls);
        Assert.Equal("Datasamling ikke valgt", DatasamlingCell(Rows(cut)[0]));
    }
}
