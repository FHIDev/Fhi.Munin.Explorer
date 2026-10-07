using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The one place the circuit's saved lists live. Two things are load-bearing here: the surfaces
/// sharing a circuit see each other's changes without refetching, and a signed-out reader costs
/// the API nothing at all.
/// </summary>
public class VariableListStateTest : ExplorerTestContext
{
    /// <summary>Counts what actually reached the API, which is the only honest witness for the trap below.</summary>
    private sealed class CountingClient : EmptyMuninExplorerClient
    {
        public int GetMyListsCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public int RenameCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public int AddCalls { get; private set; }
        public int RemoveCalls { get; private set; }

        public int TotalMyListsCalls =>
            GetMyListsCalls + CreateCalls + RenameCalls + DeleteCalls + AddCalls + RemoveCalls;

        private readonly List<VariableList> _lists = [];

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            GetMyListsCalls++;
            return Task.FromResult<IReadOnlyList<VariableList>>([.. _lists]);
        }

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            var created = new VariableList { Id = Guid.NewGuid(), Name = name };
            _lists.Add(created);
            return Task.FromResult(created);
        }

        public override Task<bool> RenameMyListAsync(Guid id, string name, CancellationToken cancellationToken = default)
        {
            RenameCalls++;
            return Task.FromResult(true);
        }

        public override Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return Task.FromResult(true);
        }

        /// <summary>Drops a list from the answers, as a delete in another tab would.</summary>
        public void Forget(Guid id) => _lists.RemoveAll(l => l.Id == id);

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            return Task.FromResult(true);
        }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            return Task.FromResult(true);
        }
    }

    private static VariableListState SignedIn(IMuninExplorerClient client)
    {
        var state = new VariableListState(client);
        state.SetAuthenticated(true);
        return state;
    }

    /// <summary>Fails the lists read until told to answer.</summary>
    private sealed class FlakyListsClient : EmptyMuninExplorerClient
    {
        public bool Fail { get; set; } = true;

        /// <summary>Leave the next read in flight instead of answering it.</summary>
        public bool Hold { get; set; }

        /// <summary>Fail the way HttpClient's own timeout does: a cancellation nobody asked for.</summary>
        public bool TimeOut { get; set; }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Hold ? Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith<IReadOnlyList<VariableList>>(
                    _ => [], cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default)
            : TimeOut ? Task.FromException<IReadOnlyList<VariableList>>(new TaskCanceledException("timeout"))
            : Fail ? Task.FromException<IReadOnlyList<VariableList>>(new HttpRequestException("nede"))
            : Task.FromResult<IReadOnlyList<VariableList>>([]);
    }

    [Fact]
    public async Task ListsReadFailed_WhileARetryIsInFlight_ThenItIsNotStillSaid()
    {
        // Otherwise "Henter …" and "Kunne ikke hente" stand side by side for the whole retry.
        var client = new FlakyListsClient();
        var state = SignedIn(client);

        await Assert.ThrowsAsync<HttpRequestException>(() => state.EnsureLoadedAsync());
        client.Hold = true;
        _ = state.EnsureLoadedAsync();

        Assert.True(state.IsReadingLists);
        Assert.False(state.ListsReadFailed);
    }

    [Fact]
    public async Task ListsReadFailed_WhenTheReadTimesOut_ThenItIsSaidAndEverySurfaceIsTold()
    {
        // HttpClient's timeout is a TaskCanceledException; the caller cancelled nothing.
        var state = SignedIn(new FlakyListsClient { TimeOut = true });
        var toldItEnded = false;
        state.Changed += _ => toldItEnded |= !state.IsReadingLists;

        await Assert.ThrowsAsync<TaskCanceledException>(() => state.EnsureLoadedAsync());

        Assert.True(state.ListsReadFailed);
        Assert.True(toldItEnded);
    }

    [Fact]
    public async Task EnsureLoaded_WhenARetryStarts_ThenEverySurfaceIsToldBeforeItAnswers()
    {
        // A view still showing the last failure has to learn the retry began, not only how it ended.
        var client = new FlakyListsClient();
        var state = SignedIn(client);
        await Assert.ThrowsAsync<HttpRequestException>(() => state.EnsureLoadedAsync());

        VariableListState.ListChange? told = null;
        state.Changed += change => told = change;
        client.Hold = true;
        _ = state.EnsureLoadedAsync();

        Assert.True(state.IsReadingLists);
        Assert.Equal(new VariableListState.ListChange(null, AffectsRows: false), told);
    }

    [Fact]
    public async Task EnsureLoaded_WhenTheCallerCancels_ThenEverySurfaceIsToldTheReadEnded()
    {
        var state = SignedIn(new FlakyListsClient { Hold = true });
        using var cancel = new CancellationTokenSource();
        var toldItEnded = false;
        state.Changed += _ => toldItEnded |= !state.IsReadingLists;

        var read = state.EnsureLoadedAsync(cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        Assert.False(state.IsReadingLists);
        Assert.True(toldItEnded);
    }

    [Fact]
    public async Task ListsReadFailed_WhenTheCallerCancels_ThenItIsNotCalledAFailure()
    {
        var state = SignedIn(new FlakyListsClient { TimeOut = true });
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => state.EnsureLoadedAsync(cancelled.Token));

        Assert.False(state.ListsReadFailed);
    }

    /// <summary>Holds the lists read until told, and makes lists the held answer never mentions.</summary>
    private sealed class HeldListsClient : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource<IReadOnlyList<VariableList>> _lists = new();

        public void Answer() => _lists.SetResult([]);

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            _lists.Task;

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VariableList { Id = Guid.NewGuid(), Name = name });
    }

    [Fact]
    public async Task EnsureLoaded_WhenAListIsMadeActiveWhileItReads_ThenItsAnswerDoesNotForgetIt()
    {
        // A save made before the mount's read lands creates and activates a list that read never saw.
        var client = new HeldListsClient();
        var state = SignedIn(client);
        var read = state.EnsureLoadedAsync();
        var made = await state.CreateAsync("Ny");
        await state.SetActiveListAsync(made!.Id);

        client.Answer();
        await read;

        Assert.Equal(made.Id, state.ActiveListId);
    }

    [Fact]
    public async Task EnsureLoaded_WhenTheActiveListIsNoLongerAmongTheLists_ThenItIsForgotten()
    {
        // Deleted in another tab: kept active, the view would ask for a list the API no longer has.
        var client = new CountingClient();
        var state = SignedIn(client);
        var gone = await state.CreateAsync("Borte");
        await state.SetActiveListAsync(gone!.Id);
        await client.DeleteMyListAsync(gone.Id);
        client.Forget(gone.Id);

        await state.RefreshAsync();

        Assert.Null(state.ActiveListId);
    }

    [Fact]
    public async Task ListsReadFailed_WhenALaterReadSucceeds_ThenItIsCleared()
    {
        // Left set, an empty answer would be taken for an unread one, and the reader told nothing.
        var client = new FlakyListsClient();
        var state = SignedIn(client);

        await Assert.ThrowsAsync<HttpRequestException>(() => state.EnsureLoadedAsync());
        Assert.True(state.ListsReadFailed);

        client.Fail = false;
        await state.EnsureLoadedAsync();

        Assert.False(state.ListsReadFailed);
    }

    [Fact]
    public async Task ListsReadFailed_WhenTheReaderSignsOutAndIn_ThenItIsCleared()
    {
        // The failure was the previous reader's read; the next reader's lists are not yet asked for.
        var state = SignedIn(new FlakyListsClient());

        await Assert.ThrowsAsync<HttpRequestException>(() => state.EnsureLoadedAsync());
        state.SetAuthenticated(false);
        state.SetAuthenticated(true);

        Assert.False(state.ListsReadFailed);
    }

    // -----------------------------------------------------------------------
    // The trap: a signed-out reader must cost the API nothing.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task State_WhenTheReaderIsSignedOut_ThenNotOneCallReachesMyLists()
    {
        // Asserted on the call count, not on what is displayed. An implementation that calls and
        // swallows the 401 looks identical on screen and sends a failed request every render.
        var client = new CountingClient();
        var state = new VariableListState(client);

        await state.EnsureLoadedAsync();
        await state.RefreshAsync();
        await state.CreateAsync("Mine hjertevariabler");
        await state.RenameAsync(Guid.NewGuid(), "Nytt navn");
        await state.DeleteAsync(Guid.NewGuid());
        await state.AddVariablesAsync(Guid.NewGuid(), [Guid.NewGuid()]);
        await state.RemoveVariablesAsync(Guid.NewGuid(), [Guid.NewGuid()]);

        Assert.Equal(0, client.TotalMyListsCalls);
        Assert.Empty(state.Lists);
    }

    [Fact]
    public void State_WhenNobodyHasSaidAnything_ThenTheReaderCountsAsSignedOut()
    {
        // A host that forgets the parameter must fail by showing no lists, never by calling
        // unauthorised on the reader's behalf.
        Assert.False(new VariableListState(new CountingClient()).IsAuthenticated);
    }

    [Fact]
    public void Component_WhenTheHostSetsNoIsAuthenticated_ThenTheStateStaysSignedOut()
    {
        var client = new CountingClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        Render<VariableSearch>();

        var state = Services.GetRequiredService<VariableListState>();
        Assert.False(state.IsAuthenticated);
        Assert.Equal(0, client.TotalMyListsCalls);
    }

    [Fact]
    public void Component_WhenTheHostSaysSignedIn_ThenTheStateAgrees()
    {
        Services.AddSingleton<IMuninExplorerClient>(new CountingClient());
        Services.AddScoped<VariableListState>();

        Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, true));

        Assert.True(Services.GetRequiredService<VariableListState>().IsAuthenticated);
    }

    [Fact]
    public void Component_WhenTheStateIsNotRegistered_ThenTheExplorerStillRenders()
    {
        // Same tolerance the package extends to a host with no localisation services: a host that
        // never called AddMuninExplorer loses saved lists, not the explorer.
        Services.AddSingleton<IMuninExplorerClient>(new CountingClient());

        var cut = Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, true));

        Assert.NotEmpty(cut.Markup);
    }

    // -----------------------------------------------------------------------
    // One holder, so the surfaces agree.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task State_WhenOneSurfaceAddsAList_ThenAnotherSeesItWithoutFetchingAgain()
    {
        var client = new CountingClient();
        var state = SignedIn(client);

        await state.EnsureLoadedAsync();
        var afterLoad = client.GetMyListsCalls;

        await state.CreateAsync("Mine hjertevariabler");

        Assert.Contains(state.Lists, l => l.Name == "Mine hjertevariabler");
        Assert.Equal(afterLoad, client.GetMyListsCalls);
    }

    [Fact]
    public async Task State_WhenAListChanges_ThenEverySurfaceIsToldOnce()
    {
        var state = SignedIn(new CountingClient());
        var notifications = 0;
        state.Changed += _ => notifications++;

        await state.CreateAsync("Hjerte og kar");

        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task State_WhenALoadHasHappened_ThenASecondSurfaceDoesNotRepeatIt()
    {
        var client = new CountingClient();
        var state = SignedIn(client);

        await state.EnsureLoadedAsync();
        await state.EnsureLoadedAsync();

        Assert.Equal(1, client.GetMyListsCalls);
    }

    [Fact]
    public async Task State_WhenRefreshIsAsked_ThenItReadsAgainRatherThanServingTheCachedCopy()
    {
        var client = new CountingClient();
        var state = SignedIn(client);

        await state.EnsureLoadedAsync();
        await state.RefreshAsync();

        Assert.Equal(2, client.GetMyListsCalls);
    }

    [Fact]
    public async Task State_WhenAListIsRenamed_ThenTheNewNameIsHeldWithoutARoundTrip()
    {
        var client = new CountingClient();
        var state = SignedIn(client);
        var created = await state.CreateAsync("Feil navn");

        var renamed = await state.RenameAsync(created!.Id, "Riktig navn");

        Assert.True(renamed);
        Assert.Contains(state.Lists, l => l.Id == created.Id && l.Name == "Riktig navn");
        Assert.Equal(0, client.GetMyListsCalls);
    }

    [Fact]
    public async Task State_WhenAListIsDeleted_ThenItLeavesTheHeldCopyToo()
    {
        var state = SignedIn(new CountingClient());
        var created = await state.CreateAsync("Midlertidig");

        Assert.True(await state.DeleteAsync(created!.Id));
        Assert.DoesNotContain(state.Lists, l => l.Id == created.Id);
    }

    [Fact]
    public async Task State_WhenTheReaderSignsOut_ThenTheLoadedListsAreDropped()
    {
        // Leaving the previous reader's list names on screen would be a disclosure, not staleness.
        var state = SignedIn(new CountingClient());
        await state.CreateAsync("Mine hjertevariabler");
        Assert.NotEmpty(state.Lists);

        state.SetAuthenticated(false);

        Assert.Empty(state.Lists);
    }

    [Fact]
    public async Task State_WhenAnEmptyBatchIsSent_ThenItStillReachesTheApi()
    {
        // The client documents an empty collection as a legitimate call whose answer says whether
        // the list exists, so the holder must not quietly swallow it.
        var client = new CountingClient();
        var state = SignedIn(client);

        await state.AddVariablesAsync(Guid.NewGuid(), []);

        Assert.Equal(1, client.AddCalls);
    }

    /// <summary>A client that lets the test decide when the in-flight call comes back.</summary>
    private sealed class BlockingClient : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource<IReadOnlyList<VariableList>> _gate = new();

        public void Answer(params string[] names) =>
            _gate.SetResult([.. names.Select(n => new VariableList { Id = Guid.NewGuid(), Name = n })]);

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            _gate.Task;
    }

    [Fact]
    public async Task State_WhenTheReaderSignsOutWhileTheLoadIsInFlight_ThenTheAnswerIsDiscarded()
    {
        // The disclosure the sign-out prevents, arriving a few milliseconds late: without the
        // generation check the continuation writes the previous reader's names back over the empty
        // list the sign-out just installed.
        var client = new BlockingClient();
        var state = new VariableListState(client);
        state.SetAuthenticated(true);

        var inFlight = state.EnsureLoadedAsync();
        state.SetAuthenticated(false);
        client.Answer("Mine hjertevariabler");
        await inFlight;

        Assert.Empty(state.Lists);
    }

    /// <summary>Holds every lists read open on its own gate, so a stale read and a fresh one can be told apart.</summary>
    private sealed class QueuedListsClient : EmptyMuninExplorerClient
    {
        private readonly List<TaskCompletionSource<IReadOnlyList<VariableList>>> _reads = [];

        public int Calls => _reads.Count;

        public void Answer(int read, params string[] names) =>
            _reads[read].SetResult([.. names.Select(n => new VariableList { Id = Guid.NewGuid(), Name = n })]);

        public void Fail(int read, Exception? failure = null) =>
            _reads[read].SetException(failure ?? new HttpRequestException("the API is down"));

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            var read = new TaskCompletionSource<IReadOnlyList<VariableList>>();
            _reads.Add(read);
            return read.Task;
        }
    }

    [Fact]
    public async Task EnsureLoaded_WhenTheReaderSignsOutAndInWhileTheListsAreRead_ThenTheNewReadersReadStarts()
    {
        var client = new QueuedListsClient();
        var state = SignedIn(client);
        var stale = state.EnsureLoadedAsync();

        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        var fresh = state.EnsureLoadedAsync();

        Assert.Equal(2, client.Calls);
        Assert.True(state.IsReadingLists);

        client.Answer(1, "Den nye leserens liste");
        await fresh;
        client.Answer(0, "Den forrige leserens liste");
        await stale;

        Assert.Equal("Den nye leserens liste", Assert.Single(state.Lists).Name);
    }

    [Fact]
    public async Task EnsureLoaded_WhenTheStaleReadFinishesFirst_ThenTheNewReadIsStillJoinedRatherThanSentAgain()
    {
        // The stale read clearing the in-flight mark would let the next surface to mount send a second read.
        var client = new QueuedListsClient();
        var state = SignedIn(client);
        var stale = state.EnsureLoadedAsync();
        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        var fresh = state.EnsureLoadedAsync();
        Assert.Equal(2, client.Calls);

        client.Answer(0, "Den forrige leserens liste");
        await stale;
        var joined = state.EnsureLoadedAsync();

        Assert.Equal(2, client.Calls);
        Assert.True(state.IsReadingLists);

        client.Answer(1, "Den nye leserens liste");
        await Task.WhenAll(fresh, joined);
        Assert.False(state.IsReadingLists);
    }

    [Fact]
    public async Task EnsureLoaded_WhenTheStaleReadFailsAfterTheNewReadersListsLanded_ThenItsCallerIsNotToldOfIt()
    {
        // Thrown, it reaches the view that started it, which would replace the new reader's lists with an error.
        var client = new QueuedListsClient();
        var state = SignedIn(client);
        var stale = state.EnsureLoadedAsync();
        state.SetAuthenticated(false);
        state.SetAuthenticated(true);
        var fresh = state.EnsureLoadedAsync();
        client.Answer(1, "Den nye leserens liste");
        await fresh;

        client.Fail(0);
        await stale;

        Assert.False(state.ListsReadFailed);
        Assert.Equal("Den nye leserens liste", Assert.Single(state.Lists).Name);
    }

    [Theory]
    [InlineData(true, LogLevel.Warning)]
    [InlineData(false, LogLevel.Error)]
    public async Task EnsureLoaded_WhenTheStaleReadFails_ThenItIsLoggedAtTheLevelARefusalOrAFaultGets(bool refused, LogLevel expected)
    {
        // Dropped from the reader's view, but a fault must still reach the host's error telemetry.
        var recorder = new RecordingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(recorder));
        var client = new QueuedListsClient();
        var state = new VariableListState(client, factory.CreateLogger<VariableListState>());
        state.SetAuthenticated(true);
        var stale = state.EnsureLoadedAsync();
        state.SetAuthenticated(false);
        state.SetAuthenticated(true);

        client.Fail(0, refused ? new MuninExplorerRateLimitedException() : new HttpRequestException("the API is down"));
        await stale;

        Assert.Equal(expected, Assert.Single(recorder.Entries).Level);
    }

    /// <summary>A client whose add is held open until the test lets it finish.</summary>
    private sealed class BlockingAddClient : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource<bool> _gate = new();

        public void Answer() => _gate.SetResult(true);

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = Guid.NewGuid(), Name = "Mine" }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [],
                TotalCount = 0,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1
            });

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default) =>
            _gate.Task;
    }

    [Fact]
    public async Task State_WhenTheReaderSignsOutWhileASaveIsInFlight_ThenTheVariableIsNotPutBack()
    {
        // The same disclosure as the list names, one layer down: the add succeeds on the server
        // because it went out under the old token, and without the generation guard the
        // continuation would put that reader's variable back into the set the sign-out cleared.
        var client = new BlockingAddClient();
        var state = new VariableListState(client);
        state.SetAuthenticated(true);
        var variableId = Guid.NewGuid();

        var inFlight = state.ToggleSavedAsync(variableId, "Min variabelliste");
        state.SetAuthenticated(false);
        client.Answer();
        await inFlight;

        Assert.False(state.IsSaved(variableId));
    }

    [Fact]
    public async Task State_WhenTheReaderSignsOutWhileABatchAddIsInFlight_ThenTheVariableIsNotPutBack()
    {
        // The same guard as the press above, at the level it delegates to. Asserted on
        // AddVariablesAsync directly: the list view and the download reach it without going
        // through ToggleSavedAsync, so a guard held only on the press would not hold for them.
        var client = new BlockingAddClient();
        var state = new VariableListState(client);
        state.SetAuthenticated(true);
        await state.EnsureActiveListAsync();

        var listId = state.ActiveListId!.Value;
        var variableId = Guid.NewGuid();

        var inFlight = state.AddVariablesAsync(listId, [variableId]);
        state.SetAuthenticated(false);
        client.Answer();
        await inFlight;

        // And the reader who arrives next must not inherit it either: signing back in is a new
        // generation, so nothing the previous reader's call carried may still be in the set.
        state.SetAuthenticated(true);

        Assert.False(state.IsSaved(variableId));
    }

    [Fact]
    public async Task State_WhenAVariableIsRemovedFromTheActiveList_ThenItReadsAsUnsaved()
    {
        // Fhi.Metadata-ehghv: VariableListView removes through this method rather than through
        // ToggleSavedAsync, and the set used to be maintained only on the press — so the search
        // row went on offering to remove a variable that had already gone.
        var variableId = Guid.NewGuid();
        var client = new MembershipClient(variableId);
        var state = SignedIn(client);
        await state.EnsureActiveListAsync();

        Assert.True(state.IsSaved(variableId));

        Assert.True(await state.RemoveVariablesAsync(state.ActiveListId!.Value, [variableId]));
        Assert.False(state.IsSaved(variableId));

        // No second walk of the list: the holder applied the change it just made.
        Assert.Equal(1, client.MembershipCalls);
    }

    [Fact]
    public async Task State_WhenAWriteAddressesAnotherList_ThenTheActiveListsMembershipStands()
    {
        // The set holds the active list and nothing else. Taking a variable out of some other list
        // says nothing about the one the save buttons are drawn from, and a set that took the
        // removal anyway would draw the variable as gone from a list that still has it.
        var variableId = Guid.NewGuid();
        var client = new MembershipClient(variableId);
        var state = SignedIn(client);
        await state.EnsureActiveListAsync();

        Assert.True(await state.RemoveVariablesAsync(Guid.NewGuid(), [variableId]));

        Assert.True(state.IsSaved(variableId));

        // And no count is invented for it either. Only the active list's membership is known here,
        // so a number moved for any other list would stand until something read my/lists again.
        Assert.Equal(1, state.Lists[0].VariableCount);
    }

    [Fact]
    public async Task State_WhenVariablesAreAddedAndRemoved_ThenTheListsOwnCountMovesWithThem()
    {
        // my/lists carries the count now, and nothing refetches it after a write — the holder
        // patches its copy, which is what makes a save feel instant. So a count read at the mount
        // would be the number every surface showed for the rest of the circuit.
        var alreadyThere = Guid.NewGuid();
        var client = new MembershipClient(alreadyThere, Guid.NewGuid(), Guid.NewGuid());
        var state = SignedIn(client);
        await state.EnsureActiveListAsync();

        var listId = state.ActiveListId!.Value;

        Assert.Equal(3, state.Lists[0].VariableCount);

        Assert.True(await state.AddVariablesAsync(listId, [Guid.NewGuid(), Guid.NewGuid()]));
        Assert.Equal(5, state.Lists[0].VariableCount);

        Assert.True(await state.RemoveVariablesAsync(listId, [alreadyThere]));
        Assert.Equal(4, state.Lists[0].VariableCount);
    }

    [Fact]
    public async Task State_WhenAVariableTheListAlreadyHoldsIsAddedAgain_ThenTheCountStandsStill()
    {
        // The API accepts it and stores nothing — the batch is a set, not a tally. A count moved by
        // variableIds.Count would climb past what the list holds and stay there, and the reader has
        // no way to press it back down. The delta is measured off the membership set instead.
        var alreadyThere = Guid.NewGuid();
        var client = new MembershipClient(alreadyThere);
        var state = SignedIn(client);
        await state.EnsureActiveListAsync();

        Assert.True(await state.AddVariablesAsync(state.ActiveListId!.Value, [alreadyThere]));

        Assert.Equal(1, state.Lists[0].VariableCount);
        Assert.Single(client.Stored);
    }

    /// <summary>One list the reader already has, holding whatever the test seeded it with.</summary>
    private sealed class MembershipClient(params Guid[] stored) : EmptyMuninExplorerClient
    {
        private static readonly Guid TheList = Guid.NewGuid();

        public int MembershipCalls { get; private set; }

        public readonly HashSet<Guid> Stored = [.. stored];

        // VariableCount off Stored, never a number of its own: the API answers the same total for
        // this endpoint and for the variables endpoint, so a fake that let them differ would be
        // testing against a server that cannot exist.
        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = TheList, Name = "Mine", VariableCount = Stored.Count }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            MembershipCalls++;

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. Stored.Select(v => new VariableListItem { VariableId = v })],
                TotalCount = Stored.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1
            });
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            if (id == TheList) { foreach (var v in variableIds) { Stored.Add(v); } }

            return Task.FromResult(true);
        }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            if (id == TheList) { foreach (var v in variableIds) { Stored.Remove(v); } }

            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task State_WhenThreeSurfacesMountTogether_ThenTheListsAreReadOnce()
    {
        var client = new CountingClient();
        var state = SignedIn(client);

        await Task.WhenAll(state.EnsureLoadedAsync(), state.EnsureLoadedAsync(), state.EnsureLoadedAsync());

        Assert.Equal(1, client.GetMyListsCalls);
    }

    // -----------------------------------------------------------------------
    // The trap: a membership read that was refused has to be readable again —
    // without retrying at every render, and without the press that repairs it
    // acting on anything but the state the reader saw.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Serves one list, and refuses the membership read until the test relents.
    /// </summary>
    /// <remarks>
    /// A 429 rather than a generic failure because that is the refusal the reader is told to wait
    /// out — and waiting is only good advice if the wait actually repairs anything.
    /// </remarks>
    private sealed class RefusingMembershipClient : EmptyMuninExplorerClient
    {
        private readonly Guid _listId = Guid.NewGuid();

        /// <summary>Refuse every membership read while set.</summary>
        public bool RateLimitMembership { get; set; } = true;

        public int MembershipCalls { get; private set; }

        /// <summary>What the list holds — written by the adds and reads, as the API's would be.</summary>
        public List<Guid> Contents { get; } = [];

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = _listId, Name = "Mine" }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            MembershipCalls++;

            if (RateLimitMembership)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. Contents.Select(v => new VariableListItem { VariableId = v })],
                TotalCount = Contents.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1
            });
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            Contents.AddRange(variableIds.Where(v => !Contents.Contains(v)));

            return Task.FromResult(true);
        }

        /// <summary>
        /// Written rather than left to the base's <see langword="false"/>, because the trap below is
        /// a delete nobody asked for: a fake that could not lose anything could not show it.
        /// </summary>
        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            Contents.RemoveAll(variableIds.Contains);

            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task State_WhenTheMembershipReadIsRefused_ThenAReaderInitiatedAskReadsItAgain()
    {
        // The reason "wait and try again" is honest advice. Picking the active list assigns the id
        // before the membership read runs, so a read that throws leaves the id set and the set
        // empty. A guard on the id alone would return from every later ask, and every variable the
        // reader had saved would render unsaved for the rest of the circuit — the label and the
        // stored list disagreeing, with nothing left that could put them right.
        var client = new RefusingMembershipClient();
        var state = SignedIn(client);
        var saved = Guid.NewGuid();

        client.Contents.Add(saved);

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.EnsureActiveListAsync());

        Assert.False(state.IsSaved(saved));
        Assert.Equal(1, client.MembershipCalls);

        // And the render that follows does not try again, however many of them there are.
        // OnParametersSetAsync runs on every parameter change, so a retry here would put a
        // multi-page membership read alongside every search and every page turn — the burst that
        // earned the 429, rebuilt by the path that is supposed to recover from it.
        client.RateLimitMembership = false;

        await state.EnsureActiveListAsync();
        await state.EnsureActiveListAsync();

        Assert.Equal(1, client.MembershipCalls);
        Assert.False(state.IsSaved(saved));

        // The reader pressing something is the ask that does read again.
        await state.ToggleSavedAsync(Guid.NewGuid(), "Min variabelliste");

        Assert.True(state.IsSaved(saved));
        Assert.Equal(2, client.MembershipCalls);

        // And once it has arrived it is not read again: the retry is for a read that failed, not a
        // read on every press.
        await state.ToggleSavedAsync(Guid.NewGuid(), "Min variabelliste");

        Assert.Equal(2, client.MembershipCalls);
    }

    [Fact]
    public async Task State_WhenASaveFollowsARefusedMembershipRead_ThenTheSaveHappensAndTheOtherRowsAreRightAgain()
    {
        // The same repair seen from the button, which is where the reader meets it. Two things have
        // to hold: the refused read must not cost the reader the save they actually asked for — the
        // list id is known, the write is one request, and losing it would make "wait and try again"
        // cost an action as well as a label — and the press after the window passes must put every
        // other row's label right as it goes.
        var client = new RefusingMembershipClient();
        var state = SignedIn(client);
        var savedEarlier = Guid.NewGuid();
        var savingNow = Guid.NewGuid();
        var savingLater = Guid.NewGuid();

        client.Contents.Add(savedEarlier);

        Assert.True(await state.ToggleSavedAsync(savingNow, "Min variabelliste"));
        Assert.Contains(savingNow, client.Contents);

        // The labels are what the refusal did cost: nothing has read the list yet.
        Assert.False(state.IsSaved(savedEarlier));

        client.RateLimitMembership = false;

        Assert.True(await state.ToggleSavedAsync(savingLater, "Min variabelliste"));
        Assert.True(state.IsSaved(savedEarlier));
        Assert.True(state.IsSaved(savingNow));
        Assert.True(state.IsSaved(savingLater));
    }

    [Fact]
    public async Task State_WhenTheFirstPressAfterARefusedReadIsOnASavedVariable_ThenItIsNotDeleted()
    {
        // The trap inside the repair. The repair runs inside the press, so the set fills in the
        // middle of the call — after the button was drawn and after the reader read it. A press
        // that then asked the freshly filled set which way to go would find this variable saved and
        // remove it: the reader presses the button labelled "save" on a variable they saved
        // yesterday, and it disappears from their list, silently, with the label flipping back to
        // "save" as though nothing had happened.
        var client = new RefusingMembershipClient();
        var state = SignedIn(client);
        var savedYesterday = Guid.NewGuid();

        client.Contents.Add(savedYesterday);

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.EnsureActiveListAsync());

        // Drawn as unsaved, because the read that would have said otherwise was refused.
        Assert.False(state.IsSaved(savedYesterday));

        client.RateLimitMembership = false;

        Assert.True(await state.ToggleSavedAsync(savedYesterday, "Min variabelliste"));

        // The direction came from the label, so the press added — which the API stores once — and
        // the variable is still the reader's.
        Assert.Contains(savedYesterday, client.Contents);
        Assert.True(state.IsSaved(savedYesterday));

        // And it is a working toggle again, now that the label and the list agree.
        Assert.False(await state.ToggleSavedAsync(savedYesterday, "Min variabelliste"));
        Assert.DoesNotContain(savedYesterday, client.Contents);
    }

    /// <summary>
    /// Serves one list over several pages, and can hold every page until the test lets it go.
    /// </summary>
    /// <remarks>
    /// Paged and gated because both hazards need a read that is still walking: two asks overlapping
    /// must produce one read rather than two, and a read that is refused partway through must
    /// publish nothing.
    /// </remarks>
    private sealed class PagedMembershipClient : EmptyMuninExplorerClient
    {
        private readonly Guid _listId = Guid.NewGuid();
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MembershipCalls { get; private set; }

        /// <summary>The list's contents, split the way the API would serve them.</summary>
        public List<Guid[]> Pages { get; } = [];

        /// <summary>Refuse this page and every later one with the API's 429. Zero refuses nothing.</summary>
        public int RefuseFromPage { get; set; }

        /// <summary>Hold every page until <see cref="Release"/>, so a test can overlap two asks.</summary>
        public bool Gated { get; init; }

        public void Release() => _gate.TrySetResult();

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = _listId, Name = "Mine" }]);

        public override async Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            MembershipCalls++;

            if (Gated)
            {
                await _gate.Task;
            }

            if (RefuseFromPage > 0 && page >= RefuseFromPage)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            var items = page <= Pages.Count ? Pages[page - 1] : [];

            return new Page<VariableListItem>
            {
                Items = [.. items.Select(v => new VariableListItem { VariableId = v })],
                TotalCount = Pages.Sum(p => p.Length),
                PageNumber = page,
                Size = pageSize,
                TotalPages = Pages.Count
            };
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    [Fact]
    public async Task State_WhenASecondAskArrivesWhileMembershipIsStillBeingRead_ThenItIsReadOnceAndInFull()
    {
        // The sibling guard EnsureLoadedAsync has had all along, for the same burst: several
        // surfaces mount together and each one asks before any of them has finished, and the flag
        // that says "membership is here" stays false for the whole length of a paged read. Without
        // this the second ask sends its own walk of the same list — a duplicate request against the
        // very limiter this work exists to be gentle with — and the two reads write over each
        // other, so the one that finishes second can latch a set with pages missing as complete.
        var onPageOne = Guid.NewGuid();
        var onPageTwo = Guid.NewGuid();
        var client = new PagedMembershipClient { Gated = true };

        client.Pages.Add([onPageOne]);
        client.Pages.Add([onPageTwo]);

        var state = SignedIn(client);

        var mounting = state.EnsureActiveListAsync();

        Assert.False(mounting.IsCompleted);
        Assert.Equal(1, client.MembershipCalls);

        // A second surface mounts while page one is still out.
        var alsoMounting = state.EnsureActiveListAsync();

        Assert.Equal(1, client.MembershipCalls);

        client.Release();

        await mounting;
        await alsoMounting;

        // Two calls: page one and page two of the one read. Not four.
        Assert.Equal(2, client.MembershipCalls);
        Assert.True(state.IsSaved(onPageOne));
        Assert.True(state.IsSaved(onPageTwo));
    }

    [Fact]
    public async Task State_WhenAMembershipReadIsRefusedPartWayThrough_ThenNoHalfReadListIsPublished()
    {
        // A set holding the pages a failed walk happened to reach answers "is this variable saved"
        // wrongly for everything it did not, and every row it did not reach draws "save" for a
        // variable the list already holds. So the pages are collected apart and swapped in whole,
        // or not at all.
        var onPageOne = Guid.NewGuid();
        var onPageTwo = Guid.NewGuid();
        var client = new PagedMembershipClient { RefuseFromPage = 2 };

        client.Pages.Add([onPageOne]);
        client.Pages.Add([onPageTwo]);

        var state = SignedIn(client);

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => state.EnsureActiveListAsync());

        Assert.False(state.IsSaved(onPageOne));
        Assert.False(state.IsSaved(onPageTwo));

        // And nothing was latched as read, so the reader's next press still repairs it.
        client.RefuseFromPage = 0;

        await state.ToggleSavedAsync(Guid.NewGuid(), "Min variabelliste");

        Assert.True(state.IsSaved(onPageOne));
        Assert.True(state.IsSaved(onPageTwo));
    }

    [Fact]
    public async Task State_WhenAnEmptyBatchIsRemoved_ThenItStillReachesTheApi()
    {
        var client = new CountingClient();
        var state = SignedIn(client);

        await state.RemoveVariablesAsync(Guid.NewGuid(), []);

        Assert.Equal(1, client.RemoveCalls);
    }
}
