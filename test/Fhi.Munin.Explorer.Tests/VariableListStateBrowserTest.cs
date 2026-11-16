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

        /// <summary>Thrown by every add, unless a stale row answers first.</summary>
        public Exception? AddThrows { get; set; }

        /// <summary>What an accepted add answers: false is Munin's 404 for a list that is gone.</summary>
        public bool AddAnswer { get; init; } = true;

        public bool DeleteThrows { get; set; }

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

            if (Adds.Count == 1 && HoldFirstAdd is { } hold)
            {
                return await hold.Task;
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
            return DeleteThrows ? throw new MuninExplorerRateLimitedException() : Task.FromResult(true);
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
    public async Task CarryBrowserListOverAsync_WhenTheDeleteFailsTooAfterALimiterRefusal_ThenTheNextPressReusesThatEmptyList()
    {
        // The limiter that refused the add can refuse the delete in the same window; a new list would be «Navn (2)».
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient { AddThrows = new MuninExplorerRateLimitedException(), DeleteThrows = true };
        var state = await SignedInAsync(browser, client);
        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));

        client.AddThrows = null;
        client.DeleteThrows = false;
        var carried = await state.CarryBrowserListOverAsync("Min variabelliste (2)");

        Assert.Equal(["Min variabelliste"], client.Created);
        Assert.Equal(2, client.Adds.Count);
        Assert.Equal(client.Adds[0].List, carried!.List!.Id);
        Assert.Empty(browser.Rows);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAnotherReaderPressesAfterAnUndeletedList_ThenTheirOwnListIsMade()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient { AddThrows = new MuninExplorerRateLimitedException(), DeleteThrows = true };
        var state = await SignedInAsync(browser, client);
        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        client.AddThrows = null;
        await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.Equal(2, client.Created.Count);
        Assert.NotEqual(client.Adds[0].List, client.Adds[1].List);
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
