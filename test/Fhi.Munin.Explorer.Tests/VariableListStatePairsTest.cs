using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Membership keyed by (variable, datasamling) in the circuit's list state (Fhi.Metadata-d07al.1).</summary>
public class VariableListStatePairsTest
{
    private static readonly Guid ListId = Guid.NewGuid();
    private static readonly Guid Variable = Guid.NewGuid();
    private static readonly Guid Lungekreft = Guid.NewGuid();
    private static readonly Guid Livmorhals = Guid.NewGuid();

    private sealed class PairClient(params VariableDatasamlingKey[] stored) : EmptyMuninExplorerClient
    {
        public HashSet<VariableDatasamlingKey> Stored { get; } = [.. stored];

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = ListId, Name = "Kreft", VariableCount = Stored.Count }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. Stored.Select(k => new VariableListItem { VariableId = k.VariableId, DatasamlingId = k.DatasamlingId })],
                TotalCount = Stored.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            Stored.RemoveWhere(k => variableIds.Contains(k.VariableId));
            return Task.FromResult(true);
        }
    }

    private static async Task<VariableListState> SignedInAsync(PairClient client)
    {
        var state = new VariableListState(client);
        state.SetAuthenticated(true);
        await state.EnsureActiveListAsync();

        return state;
    }

    [Fact]
    public async Task IsSaved_WhenTheVariableIsSavedFromOneDatasamling_ThenOnlyThatPairIsSaved()
    {
        var state = await SignedInAsync(new PairClient(new VariableDatasamlingKey(Variable, Lungekreft)));

        Assert.True(state.IsSaved(new VariableDatasamlingKey(Variable, Lungekreft)));
        Assert.False(state.IsSaved(new VariableDatasamlingKey(Variable, Livmorhals)));
        Assert.True(state.IsSaved(Variable));
    }

    [Fact]
    public async Task RemoveVariablesAsync_WhenTheOlderByVariableRemoveIsUsed_ThenEveryDatasamlingGoesAndTheCountFollows()
    {
        var state = await SignedInAsync(new PairClient(
            new VariableDatasamlingKey(Variable, Lungekreft), new VariableDatasamlingKey(Variable, Livmorhals)));

        Assert.True(await state.RemoveVariablesAsync(ListId, [Variable]));

        Assert.False(state.IsSaved(Variable));
        Assert.Equal(0, state.Lists.Single().VariableCount);
    }

    [Fact]
    public async Task ToggleSavedAsync_WhenOnePairOfTwoIsToggled_ThenTheOtherStaysAndTheCountMovesByOne()
    {
        var state = await SignedInAsync(new PairClient(
            new VariableDatasamlingKey(Variable, Lungekreft), new VariableDatasamlingKey(Variable, Livmorhals)));

        Assert.False(await state.ToggleSavedAsync(new VariableDatasamlingKey(Variable, Livmorhals), "Min liste"));

        Assert.True(state.IsSaved(new VariableDatasamlingKey(Variable, Lungekreft)));
        Assert.Equal(1, state.Lists.Single().VariableCount);
    }
}
