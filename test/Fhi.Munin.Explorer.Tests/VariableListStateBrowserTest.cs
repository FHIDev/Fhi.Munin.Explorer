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
    public async Task ToggleInBrowserListAsync_WhenTheRemovalsStatusSaysFull_ThenItReportsRemoved()
    {
        // A full list says so after any call; read first, it would tell the reader a removal was refused as full.
        var browser = new FakeBrowserList();
        var state = await TabAsync(browser);
        var row = FakeBrowserList.Row("Alder");
        await state.ToggleInBrowserListAsync(row, drawnAsSaved: false);
        browser.AnswersFull = true;

        var outcome = await state.ToggleInBrowserListAsync(row, drawnAsSaved: true);

        Assert.Equal(VariableListState.BrowserListOutcome.Removed, outcome);
        Assert.False(state.IsSaved(VariableDatasamlingKey.Of(row)));
    }

    [Fact]
    public async Task ReadBrowserListAsync_WhenTheFirstReadFoundNoBrowserForAMoment_ThenTheNextReadAsksAgain()
    {
        // A dropped circuit or a timeout answers null without the host's refusal; cached, the list would be gone for the circuit.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        browser.Failing.Add("count");
        var state = await TabAsync(browser);
        Assert.False(state.BrowserListAvailable);

        browser.Failing.Clear();
        await state.ReadBrowserListAsync();

        Assert.True(state.BrowserListAvailable);
        Assert.Equal(1, state.BrowserCount);
    }

    [Fact]
    public async Task ReadBrowserListAsync_WhenTheFirstReadKeepsGoingUnanswered_ThenEachCallReadsOnceAndOnlyTheFirstFailureIsAChange()
    {
        // A change sets off a render and a render reads again: changes after the first failure would never stop.
        var browser = new FakeBrowserList();
        browser.Failing.Add("count");
        var state = await TabAsync(browser);
        var version = state.BrowserVersion;

        for (var call = 0; call < 3; call++)
        {
            await state.ReadBrowserListAsync();
        }

        Assert.Equal(4, browser.Calls.Count(c => c.Call == "count"));
        Assert.Equal(version, state.BrowserVersion);
        Assert.False(state.BrowserListAvailable);
    }

    [Fact]
    public async Task ReadBrowserListAsync_WhenTheHostRefusedTheModule_ThenItIsNotAskedAgain()
    {
        // The host's answer for good: a read per render would ask a question that has been answered.
        var browser = new FakeBrowserList { Unreachable = true };
        var state = await TabAsync(browser);

        await state.ReadBrowserListAsync();

        Assert.Single(browser.Calls, c => c.Call == "count");
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

        /// <summary>The 400 also names the refused (variable, datasamling) pairs, as current Munin does.</summary>
        public bool NamesPairs { get; init; }

        /// <summary>Pairs the 400 names besides the stale ones, which this press never sent.</summary>
        public List<VariableDatasamlingKey> NamesAlso { get; } = [];

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
                ? new Client.ListItemsRefusedException(
                    [.. items.Where(Stale.Contains).Select(i => i.VariableId).Distinct()],
                    NamesPairs ? [.. items.Where(Stale.Contains), .. NamesAlso] : null)
                : new HttpRequestException("Each variable must be saved with one of its own datasamlinger.", null, System.Net.HttpStatusCode.BadRequest);
        }

        public override Task<bool> RenameMyListAsync(Guid id, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

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

    private static (FakeBrowserList Browser, ListsClient Client, VariableListItem Stale) PairRefusal(ListsClient? client = null)
    {
        var (browser, named, _) = NamedRefusal();
        client ??= new ListsClient { NamesStale = true, NamesPairs = true };
        client.Stale.UnionWith(named.Stale);
        return (browser, client, browser.Rows[1]);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenMuninNamesTheRefusedPairs_ThenTheRestIsSavedAndOnlyTheRefusedRowStays()
    {
        // The accepted row of the refused variable is saved too: Munin refused a pair, not the variable.
        var (browser, client, stale) = PairRefusal();
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.NotNull(carried!.List);
        Assert.True(carried.LeftTheBrowser);
        Assert.Equal([3, 2], client.Adds.Select(a => a.Rows));
        Assert.All(client.Adds, a => Assert.Equal(carried.List!.Id, a.List));
        Assert.Empty(client.Deleted);
        Assert.Equal([stale.VariableId], carried.Refused!);
        Assert.Equal(1, carried.RefusedRows);
        Assert.Equal([VariableDatasamlingKey.Of(stale)], browser.Rows.Select(VariableDatasamlingKey.Of));
        Assert.False(state.BrowserCarryOverStuck);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenMuninNamesAPairThisPressNeverSent_ThenOnlyTheCarriedRefusedRowsAreCounted()
    {
        // The sentence counts rows still in this browser; a pair from elsewhere would claim a row the reader does not have.
        var (browser, client, stale) = PairRefusal();
        client.NamesAlso.Add(new VariableDatasamlingKey(Guid.NewGuid(), Guid.NewGuid()));
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.NotNull(carried!.List);
        Assert.Equal([stale.VariableId], carried.Refused!);
        Assert.Equal(1, carried.RefusedRows);
        Assert.Equal([3, 2], client.Adds.Select(a => a.Rows));
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenPressedAgainAfterAPartialSave_ThenANewListIsTriedAndTheFirstIsLeftAlone()
    {
        // AC 3: the next press starts a new list; the refused row alone is refused again and only that list is deleted.
        var (browser, client, stale) = PairRefusal();
        var state = await SignedInAsync(browser, client);
        var first = await state.CarryBrowserListOverAsync("Min variabelliste");

        var again = await state.CarryBrowserListOverAsync("Min variabelliste 2");

        Assert.Null(again!.List);
        Assert.Equal(2, client.Created.Count);
        Assert.Equal([3, 2, 1], client.Adds.Select(a => a.Rows));
        Assert.DoesNotContain(first!.List!.Id, client.Deleted);
        Assert.Single(client.Deleted);
        Assert.Equal([VariableDatasamlingKey.Of(stale)], browser.Rows.Select(VariableDatasamlingKey.Of));
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheAddOfTheRestFails_ThenTheListIsDeletedEveryRowStaysAndItThrows()
    {
        // Nothing saved is the same outcome as before the pairs were named: no list, and every row still in the browser.
        var (browser, client, _) = PairRefusal(new ListsClient
        {
            NamesStale = true,
            NamesPairs = true,
            AddThrows = new HttpRequestException("nede", null, System.Net.HttpStatusCode.InternalServerError),
        });
        var state = await SignedInAsync(browser, client);

        await Assert.ThrowsAnyAsync<Exception>(() => state.CarryBrowserListOverAsync("Min variabelliste"));

        Assert.Equal([3, 2], client.Adds.Select(a => a.Rows));
        Assert.Single(client.Deleted);
        Assert.Equal(3, browser.Rows.Count);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "removeKeys");
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheAddOfTheRestIsRefusedToo_ThenTheListIsDeletedAndItThrows()
    {
        // Munin is asked again once; a second refusal is a failure, not a named refusal the reader could act on.
        var (browser, client, _) = PairRefusal(new ListsClient { NamesStale = true, NamesPairs = true });
        client.AddThrows = new Client.ListItemsRefusedException([browser.Rows[2].VariableId], [VariableDatasamlingKey.Of(browser.Rows[2])]);
        var state = await SignedInAsync(browser, client);

        await Assert.ThrowsAsync<Client.ListItemsRefusedException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));

        Assert.Equal([3, 2], client.Adds.Select(a => a.Rows));
        Assert.Single(client.Deleted);
        Assert.Equal(3, browser.Rows.Count);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenMuninNamesEveryRowsPair_ThenNothingIsSavedAndNoSecondAddIsSent()
    {
        // With nothing left to add, a second add would only spend a write: the refusal stands as before.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient { NamesStale = true, NamesPairs = true };
        client.Stale.UnionWith(browser.Rows.Select(VariableDatasamlingKey.Of));
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.Null(carried!.List);
        Assert.Equal([2], client.Adds.Select(a => a.Rows));
        Assert.Single(client.Deleted);
        Assert.Equal(2, carried.RefusedRows);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAFullListHasRefusedPairs_ThenItTakesThreeWrites()
    {
        // AC 2: Munin allows 60 writes a minute, and a full browser list must stay well inside it.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(VariableListState.MaxBrowserListRows));
        var client = new ListsClient { NamesStale = true, NamesPairs = true };
        client.Stale.UnionWith(browser.Rows.Take(5).Select(VariableDatasamlingKey.Of));
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.NotNull(carried!.List);
        Assert.Equal(3, client.Created.Count + client.Adds.Count + client.Deleted.Count);
        Assert.Equal(5, browser.Rows.Count);
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
        Assert.Single(client.Adds);

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
        // Two mounted views can press at once where storage gives no claim: the first add landing must not offer the
        // second press's list.
        var browser = new FakeBrowserList { ClaimUnstored = true };
        browser.Rows.AddRange(Rows(2));
        var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldFirstAdd = first, HoldSecondAdd = second };
        var state = await SignedInAsync(browser, client);

        var pressing = state.CarryBrowserListOverAsync("Min variabelliste");
        var alsoPressing = state.CarryBrowserListOverAsync("Min variabelliste (2)");
        Assert.Equal(2, client.Adds.Count);

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
    public async Task CarryBrowserListOverAsync_WhenAListsReadBegunAfterTheCreateLandsAfterTheAdd_ThenTheListIsThereOnce()
    {
        // That read's answer already holds the list, so keeping it as made here as well would draw it twice.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var adding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldFirstAdd = hold, BeforeAdd = () => adding.TrySetResult() };
        var state = await SignedInAsync(browser, client);
        await state.EnsureLoadedAsync();
        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        await adding.Task;
        client.HoldLists = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = state.RefreshAsync();

        hold.SetResult(true);
        var carried = await carrying;
        client.HoldLists.SetResult();
        await reading;

        Assert.Single(state.Lists, l => l.Id == carried!.List!.Id);
    }

    [Fact]
    public async Task DeleteAsync_WhenAListsReadBegunBeforeTheCreateLandsAfterTheDelete_ThenTheListStaysGone()
    {
        // Kept as made here after its delete, the list would come back from an answer that never held it.
        var client = new ListsClient();
        var state = await SignedInAsync(new FakeBrowserList(), client);
        await state.EnsureLoadedAsync();
        client.HoldLists = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = state.RefreshAsync();
        var created = await state.CreateAsync("Ny liste");

        Assert.True(await state.DeleteAsync(created!.Id));
        client.HoldLists.SetResult();
        await reading;

        Assert.DoesNotContain(state.Lists, l => l.Id == created.Id);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAListsReadBegunDuringAFailedAddLandsAfterTheDiscard_ThenTheListStaysGone()
    {
        // That read was answered while the list existed; drawn from it, a list the reader never had would flash past.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldFirstAdd = hold };
        var state = await SignedInAsync(browser, client);
        await state.EnsureLoadedAsync();
        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        var discarded = Assert.Single(client.Adds).List;
        client.HoldLists = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = state.RefreshAsync();

        hold.SetException(new MuninExplorerRateLimitedException());
        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => carrying);
        client.HoldLists.SetResult();
        await reading;

        Assert.Equal([discarded], client.Deleted);
        Assert.DoesNotContain(state.Lists, l => l.Id == discarded);
    }

    [Fact]
    public async Task DeleteAsync_WhenAListsReadBegunBeforeItLandsAfterIt_ThenTheListStaysGone()
    {
        // The same early answer: it holds a list deleted after it was asked for.
        var client = new ListsClient();
        var existing = new VariableList { Id = Guid.NewGuid(), Name = "Gammel liste" };
        client.Made.Add(existing);
        var state = await SignedInAsync(new FakeBrowserList(), client);
        await state.EnsureLoadedAsync();
        client.HoldLists = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = state.RefreshAsync();

        Assert.True(await state.DeleteAsync(existing.Id));
        client.HoldLists.SetResult();
        await reading;

        Assert.DoesNotContain(state.Lists, l => l.Id == existing.Id);
    }

    [Fact]
    public async Task RenameAsync_WhenAListsReadBegunBeforeTheCreateLandsAfterTheRename_ThenTheNewNameStays()
    {
        // Kept as it was made, the list would come back from that read under the name the reader just changed.
        var client = new ListsClient();
        var state = await SignedInAsync(new FakeBrowserList(), client);
        await state.EnsureLoadedAsync();
        client.HoldLists = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = state.RefreshAsync();
        var created = await state.CreateAsync("Ny liste");

        Assert.True(await state.RenameAsync(created!.Id, "Hjerte og kar"));
        client.HoldLists.SetResult();
        await reading;

        Assert.Equal("Hjerte og kar", Assert.Single(state.Lists, l => l.Id == created.Id).Name);
    }

    [Fact]
    public async Task RenameAsync_WhenAListsReadBegunBeforeTheRenameAnswersTheOldName_ThenTheNewNameStays()
    {
        // The read was answered before the rename, so its old name says nothing about what the reader chose since.
        var client = new ListsClient();
        var state = await SignedInAsync(new FakeBrowserList(), client);
        var existing = await state.CreateAsync("Ny liste");
        await state.EnsureLoadedAsync();
        client.HoldLists = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = state.RefreshAsync();

        Assert.True(await state.RenameAsync(existing!.Id, "Hjerte og kar"));
        client.HoldLists.SetResult();
        await reading;

        Assert.Equal("Hjerte og kar", Assert.Single(state.Lists, l => l.Id == existing.Id).Name);

        // The next read began after the rename, so its answer speaks for the name: renamed elsewhere, that name shows.
        client.HoldLists = null;
        client.Made[0] = client.Made[0] with { Name = "Kreft" };
        await state.RefreshAsync();

        Assert.Equal("Kreft", Assert.Single(state.Lists, l => l.Id == existing.Id).Name);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheAddAndTheDeleteBothFail_ThenALaterReadStillShowsTheLeftList()
    {
        // Still marked as being carried into, the list would be filtered out of every answer that holds it.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient { AddThrows = new MuninExplorerRateLimitedException(), DeleteThrows = true };
        var state = await SignedInAsync(browser, client);
        await state.EnsureLoadedAsync();
        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));
        var left = client.Adds[0].List;

        await state.RefreshAsync();

        Assert.Contains(state.Lists, l => l.Id == left);
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
        Assert.Single(client.Deleted);

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
        Assert.Contains(browser.Pending, p => p.Call == "removeKeys");

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
        Assert.Single(client.Adds);

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
    [InlineData("named-pairs")]
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
        Assert.Single(client.Adds);

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        hold.SetException(refusal switch
        {
            "named" => new Client.ListItemsRefusedException([browser.Rows[0].VariableId]),
            "named-pairs" => new Client.ListItemsRefusedException([browser.Rows[0].VariableId], [VariableDatasamlingKey.Of(browser.Rows[0])]),
            "unnamed" => new HttpRequestException("stale", null, System.Net.HttpStatusCode.BadRequest),
            _ => new MuninExplorerRateLimitedException(),
        });

        Assert.Null(await carrying);
        Assert.Single(client.Adds);
        Assert.Empty(client.Deleted);
        Assert.Equal(2, browser.Rows.Count);
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("throttled")]
    public async Task CarryBrowserListOverAsync_WhenTheReaderChangesDuringTheSecondAdd_ThenItTidiesNothing(string answer)
    {
        // The second add, of the rows Munin did not name, belongs to the reader who pressed just as the first does.
        var browser = new FakeBrowserList();
        var valid = FakeBrowserList.Row("Alder", Guid.NewGuid());
        var stale = valid with { DatasamlingId = Guid.NewGuid() };
        browser.Rows.AddRange([valid, stale, FakeBrowserList.Row("Kjønn")]);
        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { NamesStale = true, NamesPairs = true, HoldSecondAdd = second };
        client.Stale.Add(VariableDatasamlingKey.Of(stale));
        var state = await SignedInAsync(browser, client);

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        Assert.Equal(2, client.Adds.Count);

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        if (answer == "accepted")
        {
            second.SetResult(true);
        }
        else
        {
            second.SetException(new MuninExplorerRateLimitedException());
        }

        Assert.Null(await carrying);
        Assert.Empty(client.Deleted);
        Assert.Equal(3, browser.Rows.Count);
        Assert.Null(state.ActiveListId);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "removeKeys");
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAnotherReaderSignsInWhileTheKeysAreRead_ThenNoListIsMadeForThem()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new ListsClient();
        var state = await SignedInAsync(browser, client);
        browser.Hold = true;

        // The claim goes first and is answered; the reader changes while the keys read is the call still out.
        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        Assert.Equal("claim", Assert.Single(browser.Pending).Call);
        browser.ReleaseNewest();
        await WaitForPendingAsync(browser, "keys");

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        browser.Hold = false;
        browser.ReleaseNewest();

        Assert.Null(await carrying);
        Assert.Empty(client.Created);
        Assert.Empty(client.Adds);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAnotherReaderSignsInWhileTheClaimIsTaken_ThenNothingIsReadAndTheClaimIsGivenBack()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new ListsClient();
        var state = await SignedInAsync(browser, client);
        browser.Calls.Clear();
        browser.Hold = true;

        var carrying = state.CarryBrowserListOverAsync("Min variabelliste");
        Assert.Equal("claim", Assert.Single(browser.Pending).Call);

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        browser.Hold = false;
        browser.ReleaseNewest();

        Assert.Null(await carrying);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "keys");
        Assert.Empty(client.Created);
        Assert.Null(browser.Claim);
    }

    private static async Task WaitForPendingAsync(FakeBrowserList browser, string call)
    {
        for (var tries = 0; !browser.Pending.Any(p => p.Call == call); tries++)
        {
            Assert.True(tries < 200, $"no {call} call was made");
            await Task.Delay(10);
        }
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
        Assert.Single(client.Created);

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

    // Two tabs over one browser list: the claim across them (Fhi.Metadata-emsc0.3.17).
    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAnotherTabIsCarryingTheRows_ThenThisPressMakesNoListAndKeepsThem()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListsClient { HoldFirstAdd = hold };
        var firstTab = await SignedInAsync(browser, client);
        var secondTab = await SignedInAsync(browser, client);

        var carrying = firstTab.CarryBrowserListOverAsync("Min variabelliste");
        var refused = await secondTab.CarryBrowserListOverAsync("Min variabelliste");

        Assert.True(refused!.AlreadyCarrying);
        Assert.Null(refused.List);
        Assert.Single(client.Adds);
        Assert.Equal(2, browser.Rows.Count);

        hold.SetResult(true);
        var carried = await carrying;

        Assert.NotNull(carried!.List);
        Assert.Null(browser.Claim);
        Assert.Empty(browser.Rows);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheRowsAreLeavingTheBrowser_ThenAnotherTabsPressIsStillRefused()
    {
        // Until the removal lands the rows are still stored, so a claim released earlier lets a second list be made.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient { BeforeAdd = () => browser.Hold = true };
        var firstTab = await SignedInAsync(browser, client);
        var secondTab = await SignedInAsync(browser, client);

        var carrying = firstTab.CarryBrowserListOverAsync("Min variabelliste");
        await WaitForPendingAsync(browser, "removeKeys");
        browser.Hold = false;
        var refused = await secondTab.CarryBrowserListOverAsync("Min variabelliste");

        Assert.True(refused!.AlreadyCarrying);
        Assert.Single(client.Created);

        browser.ReleaseNewest();
        Assert.NotNull((await carrying)!.List);
        Assert.Null(browser.Claim);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenAClosedTabLeftAClaimThatHasExpired_ThenThePressCarriesTheRows()
    {
        var browser = new FakeBrowserList { Claim = "a-closed-tab", ClaimExpired = true };
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient();
        var state = await SignedInAsync(browser, client);

        var carried = await state.CarryBrowserListOverAsync("Min variabelliste");

        Assert.NotNull(carried!.List);
        Assert.Single(client.Adds);
        Assert.Equal(VariableListState.CarryClaimLifetime, Assert.Single(browser.ClaimLifetimes));
    }

    [Fact]
    public void CarryClaimLifetime_WhenComparedToTheClientsPerCallLimit_ThenACreateTwoAddsAndADeleteAtTheirLimitStillHoldIt()
    {
        // A claim that lapses mid-press lets a second tab read rows still stored and make the second list.
        Assert.True(VariableListState.CarryClaimLifetime > Fhi.Munin.Explorer.Client.RequestTimeoutHandler.Default * 4);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheCarryOverFails_ThenItsClaimIsReleasedAndTheNextPressWorks()
    {
        // A claim kept after a failure would turn the reader's retry away for the claim's whole lifetime.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient { AddThrows = new MuninExplorerRateLimitedException() };
        var state = await SignedInAsync(browser, client);

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.CarryBrowserListOverAsync("Min variabelliste"));

        Assert.Null(browser.Claim);
        client.AddThrows = null;
        Assert.NotNull((await state.CarryBrowserListOverAsync("Min variabelliste"))!.List);
    }

    [Fact]
    public async Task CarryBrowserListOverAsync_WhenTheClaimCallFaults_ThenNothingIsMadeAndThePressAnswersNull()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Rows(2));
        var client = new ListsClient();
        var state = await SignedInAsync(browser, client);
        browser.ThrowingCalls.Add("claim");
        browser.Calls.Clear();

        Assert.Null(await state.CarryBrowserListOverAsync("Min variabelliste"));
        Assert.DoesNotContain(browser.Calls, c => c.Call == "keys");
        Assert.Empty(client.Created);
        Assert.Equal(2, browser.Rows.Count);
    }
}
