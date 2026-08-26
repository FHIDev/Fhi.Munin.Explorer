using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The one place the circuit's saved lists live. Two things are load-bearing here: the surfaces
/// sharing a circuit see each other's changes without refetching, and a signed-out reader costs
/// the API nothing at all.
/// </summary>
public class VariableListStateTest : BunitContext
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

    private static VariableListState SignedIn(CountingClient client)
    {
        var state = new VariableListState(client);
        state.SetAuthenticated(true);
        return state;
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

        Render<VariableExplorer>();

        var state = Services.GetRequiredService<VariableListState>();
        Assert.False(state.IsAuthenticated);
        Assert.Equal(0, client.TotalMyListsCalls);
    }

    [Fact]
    public void Component_WhenTheHostSaysSignedIn_ThenTheStateAgrees()
    {
        Services.AddSingleton<IMuninExplorerClient>(new CountingClient());
        Services.AddScoped<VariableListState>();

        Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, true));

        Assert.True(Services.GetRequiredService<VariableListState>().IsAuthenticated);
    }

    [Fact]
    public void Component_WhenTheStateIsNotRegistered_ThenTheExplorerStillRenders()
    {
        // Same tolerance the package extends to a host with no localisation services: a host that
        // never called AddMuninExplorer loses saved lists, not the explorer.
        Services.AddSingleton<IMuninExplorerClient>(new CountingClient());

        var cut = Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, true));

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
        state.Changed += () => notifications++;

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

    [Fact]
    public async Task State_WhenAnEmptyBatchIsRemoved_ThenItStillReachesTheApi()
    {
        var client = new CountingClient();
        var state = SignedIn(client);

        await state.RemoveVariablesAsync(Guid.NewGuid(), []);

        Assert.Equal(1, client.RemoveCalls);
    }
}
