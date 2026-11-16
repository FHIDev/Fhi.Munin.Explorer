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

        /// <summary>The 400 names the refused variables, as Munin's body does; otherwise the body says nothing usable.</summary>
        public bool NamesStale { get; init; }

        /// <summary>Munin's write limiter: every add after this many answers 429.</summary>
        public int? ThrottleAfter { get; set; }

        /// <summary>When set, the create waits for it, so a test can change the reader mid-call.</summary>
        public TaskCompletionSource? HoldCreate { get; init; }

        public List<(Guid List, int Rows)> Adds { get; } = [];
        public List<Guid> Deleted { get; } = [];
        public List<string> Created { get; } = [];

        public override async Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            Created.Add(name);

            if (HoldCreate is { } hold)
            {
                await hold.Task;
            }

            return new VariableList { Id = Guid.NewGuid(), Name = name };
        }

        public override async Task<bool> AddItemsToMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
            BeforeAdd?.Invoke();
            Adds.Add((id, items.Count));

            if (Adds.Count == HeldAdd && HoldFirstAdd is { } hold)
            {
                return await hold.Task;
            }

            if (Adds.Count > ThrottleAfter)
            {
                throw new MuninExplorerRateLimitedException();
            }

            if (!items.Any(Stale.Contains))
            {
                return true;
            }

            throw NamesStale
                ? new Client.ListItemsRefusedException([.. items.Where(Stale.Contains).Select(i => i.VariableId)])
                : new HttpRequestException("Each variable must be saved with one of its own datasamlinger.", null, System.Net.HttpStatusCode.BadRequest);
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

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        // An outcome of its own rather than null, so the reader is told how many rows stayed and not that it failed.
        Assert.Null(carried!.List);
        Assert.Equal(1, carried.NotCarried);
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
        Assert.Empty(client.Deleted);
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

        Assert.Null(await carrying);
        Assert.Equal(2, client.Adds.Count);
        Assert.Empty(client.Deleted);
        Assert.Equal(3, browser.Rows.Count);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "removeKeys");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CarryBrowserListOverAsync_WhenAnotherReaderSignsInAndTheBatchIsThenRefused_ThenNothingIsResentOrDeleted(bool named)
    {
        // The refusal belongs to the reader who pressed; sorting it out under the next one would write to their lists.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldFirstAdd = hold };
        var state = await SignedInAsync(browser, client);

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        while (client.Adds.Count == 0)
        {
            await Task.Delay(5);
        }

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        hold.SetException(named
            ? new Client.ListItemsRefusedException([browser.Rows[0].VariableId])
            : new HttpRequestException("stale", null, System.Net.HttpStatusCode.BadRequest));

        Assert.Null(await carrying);
        Assert.Single(client.Adds);
        Assert.Empty(client.Deleted);
        Assert.Equal(2, browser.Rows.Count);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAnotherReaderSignsInWhileTheKeysAreRead_ThenNoListIsMadeForThem()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new ListsClient();
        var state = await SignedInAsync(browser, client);
        browser.Hold = true;

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        while (browser.Pending.Count == 0)
        {
            await Task.Delay(5);
        }

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        browser.Hold = false;
        browser.ReleaseNewest();

        Assert.Null(await carrying);
        Assert.Empty(client.Created);
        Assert.Empty(client.Adds);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheReaderChangesWhileTheListIsMade_ThenNoRowIsSentToIt()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldCreate = hold };
        var state = await SignedInAsync(browser, client);

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        while (client.Created.Count == 0)
        {
            await Task.Delay(5);
        }

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        hold.SetResult();

        Assert.Null(await carrying);
        Assert.Empty(client.Adds);
        Assert.Single(browser.Rows);
    }

    private static List<VariableListItem> Rows(int count) => [.. Enumerable.Range(0, count).Select(n => FakeBrowserList.Row($"v{n}"))];

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenMuninNamesAStaleRowAmongMany_ThenTheRestIsSentOnceMore()
    {
        // Row by row, 70 rows would be 70 writes against a limiter allowing 60 a minute.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(70));
        var stale = VariableDatasamlingKey.Of(browser.Rows[40]);
        var client = new ListsClient { NamesStale = true };
        client.Stale.Add(stale);
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.Equal([70, 69], client.Adds.Select(a => a.Rows));
        Assert.Equal(1, carried!.NotCarried);
        Assert.NotNull(carried.List);
        Assert.Equal([stale], browser.Rows.Select(VariableDatasamlingKey.Of));
        Assert.Empty(client.Deleted);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheRefusalNamesNoRowAndTheBatchIsLong_ThenNothingIsSavedOneByOne()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(VariableListState.MaxRowsTriedAlone + 1));
        var client = new ListsClient();
        client.Stale.Add(VariableDatasamlingKey.Of(browser.Rows[0]));
        var state = await SignedInAsync(browser, client);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));

        Assert.Single(client.Adds);
        Assert.Single(client.Deleted);
        Assert.Equal(VariableListState.MaxRowsTriedAlone + 1, browser.Rows.Count);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheRefusalNamesNoRowAndTheBatchIsShort_ThenEachRowIsTriedAlone()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(VariableListState.MaxRowsTriedAlone));
        var client = new ListsClient();
        client.Stale.Add(VariableDatasamlingKey.Of(browser.Rows[0]));
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.Equal(VariableListState.MaxRowsTriedAlone + 1, client.Adds.Count);
        Assert.Equal(1, carried!.NotCarried);
        Assert.Single(browser.Rows);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheLimiterRefusesPartWay_ThenTheSavedRowsStayAndTheNextPressFinishesTheSameList()
    {
        // One batch refused without names, then rows alone until the limiter says no: four rows are saved by then.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(10));
        var stale = VariableDatasamlingKey.Of(browser.Rows[9]);
        var client = new ListsClient { ThrottleAfter = 5 };
        client.Stale.Add(stale);
        var state = await SignedInAsync(browser, client);

        var first = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.True(first!.CutShort);
        Assert.NotNull(first.List);
        Assert.Empty(client.Deleted);
        Assert.Equal(6, browser.Rows.Count);
        Assert.Equal(6, state.BrowserRowsToCarry);

        client.ThrottleAfter = null;
        var second = await state.CarryBrowserListOverAsync("Min variabelliste (2)");

        Assert.Equal(["Min variabelliste"], client.Created);
        Assert.Equal(first.List!.Id, second!.List!.Id);
        Assert.False(second.CutShort);
        Assert.Equal([stale], browser.Rows.Select(VariableDatasamlingKey.Of));
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheLimiterRefusesBeforeAnyRowIsSaved_ThenTheNewListIsDeletedAndItThrows()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(3));
        var client = new ListsClient { ThrottleAfter = 0 };
        var state = await SignedInAsync(browser, client);

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));

        Assert.Single(client.Deleted);
        Assert.Equal(3, browser.Rows.Count);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheOnlyRowIsRefused_ThenItIsNotOfferedAgain()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Gammel"));
        var client = new ListsClient { NamesStale = true };
        client.Stale.Add(VariableDatasamlingKey.Of(browser.Rows[0]));
        var state = await SignedInAsync(browser, client);

        await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.Equal(1, state.BrowserRefusedCount);
        Assert.Equal(0, state.BrowserRowsToCarry);
        Assert.Null(await state.CarryBrowserListOverAsync("Min variabelliste"));
        Assert.Single(client.Created);
    }

    [Fact]
    public async Task RemoveRefusedFromBrowserAsync_WhenMuninRefusedARow_ThenExactlyThatRowLeavesTheBrowser()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var stale = VariableDatasamlingKey.Of(browser.Rows[0]);
        var client = new ListsClient { NamesStale = true };
        client.Stale.Add(stale);
        var state = await SignedInAsync(browser, client);
        await state.CarryBrowserListOverAsync("Min variabelliste");
        var late = FakeBrowserList.Row("Kjønn");
        browser.Rows.Add(late);
        await browser.ChangedElsewhereAsync();

        Assert.True(await state.RemoveRefusedFromBrowserAsync());

        Assert.Equal([VariableDatasamlingKey.Of(late)], browser.Rows.Select(VariableDatasamlingKey.Of));
        Assert.Equal(0, state.BrowserRefusedCount);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenOnlyRefusedRowsRemainBesideRowsStillStored_ThenItIsNotStuck()
    {
        // Stuck means a press would save rows twice; the refused rows are not offered, so nothing would be.
        var browser = new FakeBrowserList { KeepsStored = true };
        browser.Rows.AddRange([FakeBrowserList.Row("Alder"), FakeBrowserList.Row("Gammel")]);
        var client = new ListsClient { NamesStale = true };
        client.Stale.Add(VariableDatasamlingKey.Of(browser.Rows[1]));
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.False(carried!.LeftTheBrowser);
        Assert.Equal(1, state.BrowserCount);
        Assert.False(state.BrowserCarryOverStuck);
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
