using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The browser list's state over two tabs and a refusing browser (Fhi.Metadata-emsc0.3.12).</summary>
public class VariableListStateBrowserTest
{
    private sealed class NoClient : EmptyMuninExplorerClient;

    private static async Task<VariableListState> TabAsync(FakeBrowserList browser)
    {
        var state = new VariableListState(new NoClient());
        state.OfferBrowserList(browser);
        await state.ReadBrowserListAsync();
        return state;
    }

    [Fact]
    public async Task ToggleInBrowserListAsync_WhenAnotherTabEmptiedTheList_ThenTheAddDoesNotBringTheOldRowsBack()
    {
        // The browser changes the stored list one row at a time; a state that wrote its whole copy would undo the other tab.
        var browser = new FakeBrowserList();
        var first = await TabAsync(browser);
        var second = await TabAsync(browser);
        var old = FakeBrowserList.Row("Alder");
        var added = FakeBrowserList.Row("Kjønn");

        await first.ToggleInBrowserListAsync(old, drawnAsSaved: false);
        await second.ClearBrowserListAsync();
        await first.ToggleInBrowserListAsync(added, drawnAsSaved: false);

        Assert.Equal([VariableDatasamlingKey.Of(added)], browser.Rows.Select(VariableDatasamlingKey.Of));
        Assert.False(first.IsSaved(VariableDatasamlingKey.Of(old)));
        Assert.Equal(1, first.BrowserCount);
    }

    [Fact]
    public async Task SyncBrowserListAsync_WhenAnotherTabAddsARow_ThenThisTabSeesItWithoutAPress()
    {
        var browser = new FakeBrowserList();
        var first = await TabAsync(browser);
        var second = await TabAsync(browser);
        var row = FakeBrowserList.Row("Alder");

        await first.ToggleInBrowserListAsync(row, drawnAsSaved: false);
        await browser.ChangedElsewhereAsync();

        Assert.True(second.IsSaved(VariableDatasamlingKey.Of(row)));
        Assert.Equal(1, second.BrowserCount);
    }

    [Fact]
    public async Task ToggleInBrowserListAsync_WhenTheItemCarriesTheReadersWords_ThenOnlyCatalogueFieldsAreStored()
    {
        var browser = new FakeBrowserList();
        var state = await TabAsync(browser);

        await state.ToggleInBrowserListAsync(
            FakeBrowserList.Row("Alder") with { Notes = "mitt", DesiredDataFreeText = "alt", DesiredDataType = "freeText" },
            drawnAsSaved: false);

        var row = Assert.Single(browser.Rows);
        Assert.Null(row.Notes);
        Assert.Null(row.DesiredDataFreeText);
        Assert.Null(row.DesiredDataType);
        Assert.Equal("Alder", row.VariableName);
    }

    [Fact]
    public async Task ToggleInBrowserListAsync_WhenStorageWorksAgain_ThenTheListIsKeptAgain()
    {
        var browser = new FakeBrowserList { Refuses = true };
        var state = await TabAsync(browser);

        await state.ToggleInBrowserListAsync(FakeBrowserList.Row("Alder"), drawnAsSaved: false);
        Assert.False(state.BrowserListKept);

        browser.Refuses = false;
        await state.ToggleInBrowserListAsync(FakeBrowserList.Row("Kjønn"), drawnAsSaved: false);

        Assert.True(state.BrowserListKept);
    }

    [Fact]
    public async Task ToggleInBrowserListAsync_WhenTwoPressesOverlapAndAnswerOutOfOrder_ThenBothRowsAndTheCountHold()
    {
        // Without the turn each press sends at once, and the older answer, landing last, publishes its older count.
        var browser = new FakeBrowserList();
        var state = await TabAsync(browser);
        var first = FakeBrowserList.Row("Alder");
        var second = FakeBrowserList.Row("Kjønn");
        browser.Hold = true;

        var presses = Task.WhenAll(
            state.ToggleInBrowserListAsync(first, drawnAsSaved: false),
            state.ToggleInBrowserListAsync(second, drawnAsSaved: false));
        await ReleaseUntilAsync(browser, presses);

        Assert.True(state.IsSaved(VariableDatasamlingKey.Of(first)));
        Assert.True(state.IsSaved(VariableDatasamlingKey.Of(second)));
        Assert.Equal(2, state.BrowserCount);
    }

    [Fact]
    public async Task SyncBrowserListAsync_WhenAnotherTabsChangeArrivesMidToggle_ThenNoStaleListIsPublished()
    {
        // The re-read was asked before the press; answered after it, without the turn it would put back the list before it.
        var browser = new FakeBrowserList();
        var state = await TabAsync(browser);
        var row = FakeBrowserList.Row("Alder");
        browser.Hold = true;

        var sync = browser.ChangedElsewhereAsync();
        var press = state.ToggleInBrowserListAsync(row, drawnAsSaved: false);
        await ReleaseUntilAsync(browser, Task.WhenAll(sync, press));

        Assert.True(state.IsSaved(VariableDatasamlingKey.Of(row)));
        Assert.Equal(1, state.BrowserCount);
    }

    // Answers the newest held call until the work is done, as a network that delivers the later answer first.
    private static async Task ReleaseUntilAsync(FakeBrowserList browser, Task work)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!work.IsCompleted)
        {
            Assert.True(DateTime.UtcNow < deadline, "the presses never finished: a call is waiting on one nobody releases");

            if (browser.Pending.Count > 0)
            {
                browser.ReleaseNewest();
            }

            await Task.Delay(5);
        }

        await work;
    }

    private sealed class ListsClient : EmptyMuninExplorerClient
    {
        public Action? BeforeAdd { get; init; }

        /// <summary>Rows Munin refuses, as it does a datasamling that is no longer the variable's: a 400 for any batch holding one.</summary>
        public HashSet<VariableDatasamlingKey> Stale { get; } = [];

        /// <summary>When set, the add numbered <see cref="HeldAdd"/> waits for it, so a test can change the reader mid-call.</summary>
        public TaskCompletionSource<bool>? HoldFirstAdd { get; init; }

        public int HeldAdd { get; init; } = 1;

        public List<(Guid List, int Rows)> Adds { get; } = [];
        public List<Guid> Deleted { get; } = [];

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VariableList { Id = Guid.NewGuid(), Name = name });

        public override async Task<bool> AddItemsToMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
            BeforeAdd?.Invoke();
            Adds.Add((id, items.Count));

            if (Adds.Count == HeldAdd && HoldFirstAdd is { } hold)
            {
                return await hold.Task;
            }

            return items.Any(Stale.Contains)
                ? throw new HttpRequestException("Each variable must be saved with one of its own datasamlinger.", null, System.Net.HttpStatusCode.BadRequest)
                : true;
        }

        public override Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Deleted.Add(id);
            return Task.FromResult(true);
        }
    }

    private static async Task<VariableListState> SignedInAsync(FakeBrowserList browser, ListsClient? client = null)
    {
        var state = new VariableListState(client ?? new ListsClient());
        state.OfferBrowserList(browser);
        await state.ReadBrowserListAsync();
        state.SetAuthenticated(true);
        return state;
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheRowsWereOnlyInThisPagesMemory_ThenTheyLeftAndNothingIsStuck()
    {
        // Storage never held them, so no other page can offer them again: not «still here», and no flag.
        var browser = new FakeBrowserList { Refuses = true };
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var state = await SignedInAsync(browser);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.True(carried!.LeftTheBrowser);
        Assert.False(state.BrowserCarryOverStuck);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheRowsAreStillStoredAndOthersRemain_ThenItIsStuck()
    {
        // Another page still finds the carried rows, and this one still shows a list to offer again.
        var browser = new FakeBrowserList { KeepsStored = true };
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var state = await SignedInAsync(browser, new ListsClient { BeforeAdd = () => browser.Rows.Add(FakeBrowserList.Row("Kjønn")) });

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.False(carried!.LeftTheBrowser);
        Assert.True(state.BrowserCarryOverStuck);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheRowsAreStillStoredButNoneShowsHere_ThenItSaysSoWithoutTheFlag()
    {
        var browser = new FakeBrowserList { KeepsStored = true };
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var state = await SignedInAsync(browser);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.False(carried!.LeftTheBrowser);
        Assert.False(state.BrowserCarryOverStuck);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenOneRowIsStale_ThenTheOthersAreCarriedAndItStaysBehind()
    {
        // A refused batch is tried a row at a time, so one stale row no longer costs the reader the whole list.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Alder"), FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Kjønn")]);
        var stale = VariableDatasamlingKey.Of(browser.Rows[1]);
        var client = new ListsClient();
        client.Stale.Add(stale);
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.Equal(1, carried!.NotCarried);
        Assert.Equal([stale], browser.Rows.Select(VariableDatasamlingKey.Of));
        Assert.Empty(client.Deleted);
        Assert.False(state.BrowserCarryOverStuck);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenEveryRowIsStale_ThenTheNewListIsDeletedAndNothingLeaves()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Gammel"));
        var client = new ListsClient();
        client.Stale.Add(VariableDatasamlingKey.Of(browser.Rows[0]));
        var state = await SignedInAsync(browser, client);

        Assert.Null(await state.CarryBrowserListOverAsync("Min variabelliste"));

        Assert.Single(client.Deleted);
        Assert.Single(browser.Rows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CarryBrowserListOverAsync_WhenTheReaderChangesMidway_ThenItStopsAndLeavesTheBrowserRows(bool anotherSignsIn)
    {
        // The rows and the list belong to the reader who pressed: no further batch, no removal, no list made active.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Enumerable.Range(0, IMuninExplorerClient.MaxVariablesPerBatch + 1).Select(n => FakeBrowserList.Row($"v{n}")));
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldFirstAdd = hold };
        var state = await SignedInAsync(browser, client);

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        while (client.Adds.Count == 0)
        {
            await Task.Delay(5);
        }

        state.SetAuthenticated(false);
        if (anotherSignsIn)
        {
            state.SetAuthenticated(true);
        }

        hold.SetResult(true);

        Assert.Null(await carrying);
        Assert.Single(client.Adds);
        Assert.Equal(IMuninExplorerClient.MaxVariablesPerBatch + 1, browser.Rows.Count);
        Assert.Null(state.ActiveListId);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "removeKeys");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CarryBrowserListOverAsync_WhenAnotherReaderSignsInDuringARowByRowAdd_ThenNoFurtherRowIsSent(bool rowRefused)
    {
        // The batch is refused for its stale row, so the rows go one at a time; the reader changes on the first of them.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder"), FakeBrowserList.Row("Kjønn")]);
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldFirstAdd = hold, HeldAdd = 2 };
        client.Stale.Add(VariableDatasamlingKey.Of(browser.Rows[0]));
        var state = await SignedInAsync(browser, client);

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        while (client.Adds.Count < 2)
        {
            await Task.Delay(5);
        }

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);

        if (rowRefused)
        {
            hold.SetException(new HttpRequestException("stale", null, System.Net.HttpStatusCode.BadRequest));
        }
        else
        {
            hold.SetResult(true);
        }

        var outcome = await Record.ExceptionAsync(() => carrying);

        Assert.Equal(2, client.Adds.Count);
        Assert.Equal(3, browser.Rows.Count);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "removeKeys");
        Assert.True(rowRefused ? outcome is HttpRequestException : outcome is null);
    }

    private static async Task<VariableListState> StuckAsync()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        browser.Failing.Add("removeKeys");
        var state = await SignedInAsync(browser);
        await state.CarryBrowserListOverAsync("Min variabelliste");
        Assert.True(state.BrowserCarryOverStuck);
        return state;
    }

    [Fact]
    public async Task SetAuthenticated_WhenTheReaderChanges_ThenAStuckCarryOverIsOfferedAfresh()
    {
        var state = await StuckAsync();

        state.SetAuthenticated(false);

        Assert.False(state.BrowserCarryOverStuck);
    }

    [Fact]
    public async Task ClearBrowserListAsync_WhenTheListEmpties_ThenAStuckCarryOverIsForgotten()
    {
        var state = await StuckAsync();

        await state.ClearBrowserListAsync();

        Assert.False(state.BrowserCarryOverStuck);
    }

    [Fact]
    public void MaxBrowserListRows_WhenCompared_ThenItIsTheExportsCeiling() =>
        Assert.Equal(Client.MuninExplorerClient.MaxExportItems, VariableListState.MaxBrowserListRows);
}
