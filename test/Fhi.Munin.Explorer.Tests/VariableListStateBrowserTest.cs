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

        /// <summary>When set, the first add waits for it, so a test can change the reader mid-call.</summary>
        public TaskCompletionSource<bool>? HoldFirstAdd { get; init; }

        /// <summary>The 400 names the refused variables, as Munin's body does; otherwise the body says nothing usable.</summary>
        public bool NamesStale { get; init; }

        /// <summary>When set, the create waits for it, so a test can change the reader mid-call.</summary>
        public TaskCompletionSource? HoldCreate { get; init; }

        /// <summary>When set, the second add waits for it: a second press under way beside the first.</summary>
        public TaskCompletionSource<bool>? HoldSecondAdd { get; init; }

        /// <summary>When set, the delete waits for it and answers what it is given.</summary>
        public TaskCompletionSource<bool>? HoldDelete { get; init; }

        /// <summary>When set, a lists read answers with the lists made before it began, once this is done.</summary>
        public TaskCompletionSource? HoldLists { get; set; }

        /// <summary>Thrown by every add, unless a stale row answers first.</summary>
        public Exception? AddThrows { get; set; }

        /// <summary>What an accepted add answers: false is Munin's 404 for a list that is gone.</summary>
        public bool AddAnswer { get; init; } = true;

        public bool DeleteThrows { get; set; }

        public List<(Guid List, int Rows)> Adds { get; } = [];
        public List<Guid> Deleted { get; } = [];
        public List<string> Created { get; } = [];

        /// <summary>The lists Munin holds, which a read of the reader's lists answers with.</summary>
        public List<VariableList> Made { get; } = [];

        public override async Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            Created.Add(name);

            if (HoldCreate is { } hold)
            {
                await hold.Task;
            }

            var list = new VariableList { Id = Guid.NewGuid(), Name = name };
            Made.Add(list);
            return list;
        }

        public override async Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<VariableList> made = [.. Made];

            if (HoldLists is { } hold)
            {
                await hold.Task;
            }

            return made;
        }

        public override async Task<bool> AddItemsToMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
            BeforeAdd?.Invoke();
            Adds.Add((id, items.Count));

            if (Adds.Count == 1 && HoldFirstAdd is { } hold)
            {
                return await hold.Task;
            }

            if (Adds.Count == 2 && HoldSecondAdd is { } second)
            {
                return await second.Task;
            }

            if (!items.Any(Stale.Contains))
            {
                return AddThrows is { } thrown ? throw thrown : AddAnswer;
            }

            throw NamesStale
                ? new Client.ListItemsRefusedException([.. items.Where(Stale.Contains).Select(i => i.VariableId)])
                : new HttpRequestException("Each variable must be saved with one of its own datasamlinger.", null, System.Net.HttpStatusCode.BadRequest);
        }

        public override Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Deleted.Add(id);
            return HoldDelete is { } hold ? hold.Task
                : DeleteThrows ? throw new MuninExplorerRateLimitedException() : Task.FromResult(true);
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

    private static List<VariableListItem> Rows(int count) => [.. Enumerable.Range(0, count).Select(n => FakeBrowserList.Row($"v{n}"))];

    // Two rows of one variable, the second from a datasamling no longer its own, beside a row of another variable.
    private static (FakeBrowserList Browser, ListsClient Client, VariableListItem Other) NamedRefusal()
    {
        var browser = new FakeBrowserList();
        var valid = FakeBrowserList.Row("Alder", Guid.NewGuid());
        var stale = valid with { DatasamlingId = Guid.NewGuid() };
        var other = FakeBrowserList.Row("Kjønn");
        browser.Rows.AddRange([valid, stale, other]);
        var client = new ListsClient { NamesStale = true };
        client.Stale.Add(VariableDatasamlingKey.Of(stale));
        return (browser, client, other);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenMuninNamesAVariable_ThenNothingIsSavedTheListIsDeletedAndEveryRowStays()
    {
        var (browser, client, other) = NamedRefusal();
        var variable = browser.Rows[0].VariableId;
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        // One add for every row, so Munin saves all or none; both rows of the named variable are counted.
        Assert.Equal([3], client.Adds.Select(a => a.Rows));
        Assert.Null(carried!.List);
        Assert.Equal([variable], carried.Refused!);
        Assert.Equal(2, carried.RefusedRows);
        Assert.Single(client.Deleted);
        Assert.Equal(3, browser.Rows.Count);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "removeKeys");

        Assert.True(await state.RemoveRefusedFromBrowserAsync(carried.Refused!));

        Assert.Equal([VariableDatasamlingKey.Of(other)], browser.Rows.Select(VariableDatasamlingKey.Of));
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheRefusedRowsAreRemoved_ThenTheNextPressSavesTheRest()
    {
        var (browser, client, _) = NamedRefusal();
        var state = await SignedInAsync(browser, client);
        var refused = await state.CarryBrowserListOverAsync("Min variabelliste");
        await state.RemoveRefusedFromBrowserAsync(refused!.Refused!);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.NotNull(carried!.List);
        Assert.Equal([3, 1], client.Adds.Select(a => a.Rows));
        Assert.Empty(browser.Rows);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheRefusedRowsAreKept_ThenTheNextPressIsRefusedTheSameWay()
    {
        // Refused rows are not remembered: the next press sends them again, and Munin refuses them again.
        var (browser, client, _) = NamedRefusal();
        var state = await SignedInAsync(browser, client);
        await state.CarryBrowserListOverAsync("Min variabelliste");

        var again = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.Null(again!.List);
        Assert.Equal(2, again.RefusedRows);
        Assert.Equal([3, 3], client.Adds.Select(a => a.Rows));
        Assert.Equal(2, client.Deleted.Count);
        Assert.Equal(3, browser.Rows.Count);
    }

    [Theory]
    [InlineData("unnamed")]
    [InlineData("named-elsewhere")]
    [InlineData("throttled")]
    [InlineData("failed")]
    public async Task CarryBrowserListOverAsync_WhenTheAddFailsWithoutNamingARow_ThenTheListIsDeletedEveryRowStaysAndItThrows(string failure)
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(3));
        var client = new ListsClient
        {
            AddThrows = failure switch
            {
                "unnamed" => new Client.ListItemsRefusedException(null),
                "named-elsewhere" => new Client.ListItemsRefusedException([Guid.NewGuid()]),
                "throttled" => new MuninExplorerRateLimitedException(),
                _ => new HttpRequestException("nede", null, System.Net.HttpStatusCode.InternalServerError),
            },
        };
        var state = await SignedInAsync(browser, client);

        await Assert.ThrowsAnyAsync<Exception>(() => state.CarryBrowserListOverAsync("Min variabelliste"));

        Assert.Single(client.Deleted);
        Assert.Equal(3, browser.Rows.Count);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "removeKeys");
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheNewListIsGoneBeforeTheAdd_ThenItIsDeletedAndEveryRowStays()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient { AddAnswer = false };
        var state = await SignedInAsync(browser, client);

        Assert.Null(await state.CarryBrowserListOverAsync("Min variabelliste"));

        Assert.Single(client.Deleted);
        Assert.Equal(2, browser.Rows.Count);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheDeleteFailsTooAfterALimiterRefusal_ThenTheListIsLeftAndTheNextPressMakesItsOwn()
    {
        // A press deletes only the list it made: the one left behind may have taken the reader's own rows since.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient { AddThrows = new MuninExplorerRateLimitedException(), DeleteThrows = true };
        var state = await SignedInAsync(browser, client);
        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));
        var left = client.Adds[0].List;

        client.DeleteThrows = false;
        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.CarryBrowserListOverAsync("Min variabelliste (2)"));

        Assert.Equal(["Min variabelliste", "Min variabelliste (2)"], client.Created);
        Assert.NotEqual(left, client.Adds[1].List);
        Assert.Equal([left, client.Adds[1].List], client.Deleted);
        Assert.Equal(2, browser.Rows.Count);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheAddFailsAndTheDeleteThrowsToo_ThenTheAddsFailureIsWhatTheCallerSees()
    {
        // The caller words its sentence from the type: a limiter refusing the delete must not pass for the add's own fault.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient
        {
            AddThrows = new HttpRequestException("nede", null, System.Net.HttpStatusCode.InternalServerError),
            DeleteThrows = true,
        };
        var state = await SignedInAsync(browser, client);

        await Assert.ThrowsAsync<HttpRequestException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));

        Assert.Single(client.Deleted);
        Assert.Contains(state.Lists, l => l.Id == client.Adds[0].List);
        Assert.Equal(2, browser.Rows.Count);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheAddIsOut_ThenTheNewListIsOfferedNowhereEvenAfterARead()
    {
        // Offered, it could take a save of the reader's own, which deleting it after a refusal would take with it.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldFirstAdd = hold };
        var state = await SignedInAsync(browser, client);

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        while (client.Adds.Count == 0)
        {
            await Task.Delay(5);
        }

        await state.RefreshAsync();
        Assert.Contains(client.Made, l => l.Id == client.Adds[0].List);
        Assert.DoesNotContain(state.Lists, l => l.Id == client.Adds[0].List);

        hold.SetResult(true);
        var carried = await carrying;

        Assert.Contains(state.Lists, l => l.Id == carried!.List!.Id);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTwoPressesAreOut_ThenEachListIsOfferedNowhereUntilItsOwnAddIsIn()
    {
        // Two mounted views can press at once: the first add landing must not offer the second press's list.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldFirstAdd = first, HoldSecondAdd = second };
        var state = await SignedInAsync(browser, client);

        var pressing = state.CarryBrowserListOverAsync("Min variabelliste");
        var alsoPressing = state.CarryBrowserListOverAsync("Min variabelliste (2)");
        while (client.Adds.Count < 2)
        {
            await Task.Delay(5);
        }

        first.SetResult(true);
        var carried = await pressing;
        await state.RefreshAsync();

        Assert.Contains(state.Lists, l => l.Id == carried!.List!.Id);
        Assert.DoesNotContain(state.Lists, l => l.Id == client.Adds[1].List);

        second.SetResult(true);
        var alsoCarried = await alsoPressing;

        Assert.Contains(state.Lists, l => l.Id == alsoCarried!.List!.Id);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAListsReadBegunEarlierLandsAfterAFailedDiscard_ThenTheLeftListStays()
    {
        // The read was answered before the list existed; it says nothing about a list the discard could not delete.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient { AddThrows = new MuninExplorerRateLimitedException(), DeleteThrows = true };
        var state = await SignedInAsync(browser, client);
        await state.EnsureLoadedAsync();
        client.HoldLists = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = state.RefreshAsync();

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));
        var left = client.Adds[0].List;
        Assert.Contains(state.Lists, l => l.Id == left);
        client.HoldLists.SetResult();
        await reading;

        Assert.Contains(state.Lists, l => l.Id == left);

        // The next read began after the list was left, so its answer speaks for it: deleted elsewhere, it goes.
        client.HoldLists = null;
        client.Made.Clear();
        await state.RefreshAsync();

        Assert.DoesNotContain(state.Lists, l => l.Id == left);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAListsReadBegunEarlierLandsAfterTheAdd_ThenTheCarriedListStays()
    {
        // The read was answered before the list existed; replacing Lists with it would hide the rows the reader just saved.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient();
        var state = await SignedInAsync(browser, client);
        await state.EnsureLoadedAsync();
        client.HoldLists = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = state.RefreshAsync();

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");
        client.HoldLists.SetResult();
        await reading;

        Assert.Contains(state.Lists, l => l.Id == carried!.List!.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenAListsReadBegunEarlierLandsAfterIt_ThenTheNewListStays()
    {
        // The same early answer as the carry-over's: it cannot speak for a list made after it was asked for.
        var client = new ListsClient();
        var state = await SignedInAsync(new FakeBrowserList(), client);
        await state.EnsureLoadedAsync();
        client.HoldLists = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = state.RefreshAsync();

        var created = await state.CreateAsync("Ny liste");
        client.HoldLists.SetResult();
        await reading;

        Assert.Contains(state.Lists, l => l.Id == created!.Id);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAnotherReaderSignsInWhileTheDiscardIsOut_ThenTheFailedDeleteLeavesThemNoList()
    {
        // The list the delete could not take is the pressing reader's; shown to the next one, it would be a disclosure.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var delete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { AddThrows = new MuninExplorerRateLimitedException(), HoldDelete = delete };
        var state = await SignedInAsync(browser, client);

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        while (client.Deleted.Count == 0)
        {
            await Task.Delay(5);
        }

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        delete.SetException(new MuninExplorerRateLimitedException());

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => carrying);
        Assert.DoesNotContain(state.Lists, l => l.Id == client.Adds[0].List);
    }

    [Fact]
    public async Task RemoveRefusedFromBrowserAsync_WhenAnotherTabAddedARowOfTheVariableUnseen_ThenItSaysNotRemoved()
    {
        // The removal takes out the rows this page knew of; a row of the same variable it did not know of is still there.
        var (browser, client, _) = NamedRefusal();
        var state = await SignedInAsync(browser, client);
        var refused = await state.CarryBrowserListOverAsync("Min variabelliste");
        var late = browser.Rows[0] with { DatasamlingId = Guid.NewGuid() };
        browser.Rows.Add(late);

        Assert.False(await state.RemoveRefusedFromBrowserAsync(refused!.Refused!));

        Assert.Contains(browser.Rows, r => VariableDatasamlingKey.Of(r) == VariableDatasamlingKey.Of(late));
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAnotherReaderSignsInWhileTheSavedRowsLeaveTheBrowser_ThenTheirOfferIsNotStuck()
    {
        // The rows that would not leave were the last reader's press; the next reader is offered them afresh.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        browser.Failing.Add("removeKeys");
        var state = await SignedInAsync(browser, new ListsClient { BeforeAdd = () => browser.Hold = true });

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        while (!browser.Pending.Exists(p => p.Call == "removeKeys"))
        {
            await Task.Delay(5);
        }

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        browser.Hold = false;
        await ReleaseUntilAsync(browser, carrying);

        Assert.Equal(1, state.BrowserCount);
        Assert.False(state.BrowserCarryOverStuck);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CarryBrowserListOverAsync_WhenTheReaderChangesMidway_ThenItStopsAndLeavesTheBrowserRows(bool anotherSignsIn)
    {
        // The rows and the list belong to the reader who pressed: no removal, no delete, no list made active.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(3));
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
        Assert.Equal(3, browser.Rows.Count);
        Assert.Null(state.ActiveListId);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "removeKeys");
    }

    [Theory]
    [InlineData("named")]
    [InlineData("unnamed")]
    [InlineData("throttled")]
    public async Task CarryBrowserListOverAsync_WhenAnotherReaderSignsInAndTheAddIsThenRefused_ThenNothingIsDeletedUnderThem(string refusal)
    {
        // The refusal belongs to the reader who pressed; deleting the new list under the next one would write to their lists.
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
        hold.SetException(refusal switch
        {
            "named" => new Client.ListItemsRefusedException([browser.Rows[0].VariableId]),
            "unnamed" => new HttpRequestException("stale", null, System.Net.HttpStatusCode.BadRequest),
            _ => new MuninExplorerRateLimitedException(),
        });

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

    [Fact]
    public async Task RemoveRefusedFromBrowserAsync_WhenTheRowsStayInTheStoredValue_ThenItSaysTheyWereNotRemoved()
    {
        // Another page still finds them there and would draw them, so «removed» would not be true.
        var (browser, client, _) = NamedRefusal();
        browser.KeepsStored = true;
        var state = await SignedInAsync(browser, client);
        var refused = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.False(await state.RemoveRefusedFromBrowserAsync(refused!.Refused!));
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

    [Fact]
    public void MaxBrowserListRows_WhenCompared_ThenTheWholeListFitsTheOneAddOfACarryOver() =>
        Assert.True(VariableListState.MaxBrowserListRows <= IMuninExplorerClient.MaxVariablesPerBatch);
}
