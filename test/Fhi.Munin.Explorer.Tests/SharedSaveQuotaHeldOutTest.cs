using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class SharedSaveQuotaHeldOutTest : ExplorerTestContext
{
    private sealed class RefusingClient : EmptyMuninExplorerClient
    {
        public Guid? CreatedId { get; private set; }
        public List<Guid> Deleted { get; } = [];

        public override Task<SharedList?> GetSharedListAsync(string? code, CancellationToken cancellationToken = default) =>
            Task.FromResult<SharedList?>(new SharedList("Delt liste",
            [
                new VariableListItem { VariableId = Guid.NewGuid(), AddedAt = DateTimeOffset.UtcNow, VariableName = "Variabel 1", VariableCode = "V1" },
                new VariableListItem { VariableId = Guid.NewGuid(), AddedAt = DateTimeOffset.UtcNow, VariableName = "Variabel 2", VariableCode = "V2" },
            ]));

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([]);

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            CreatedId = Guid.NewGuid();
            return Task.FromResult(new VariableList { Id = CreatedId.Value, Name = name });
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default) =>
            throw new MuninExplorerListQuotaException(ListQuotaKind.Rows, 50, "Maks 50 rader.");

        public override Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Deleted.Add(id);
            return Task.FromResult(true);
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>());
    }

    [Fact]
    public void Save_WhenMuninRefusesTheAddOfASharedListAtTheRowQuota_ThenTheEmptyListTheSaveMadeIsDeleted()
    {
        var client = new RefusingClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        this.SetRendererInfo(new RendererInfo("Server", true));

        var cut = Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, "no")
            .Add(c => c.ShareCode, "AB12CD"));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("table tbody tr").Count));

        cut.FindAll("button").Single(b => AccessibleName.Of(b) == "Lagre som min liste").Click();
        cut.FindAll("button").Single(b => AccessibleName.Of(b) == "Lagre listen").Click();

        cut.WaitForAssertion(() => Assert.NotNull(client.CreatedId));
        cut.WaitForAssertion(() => Assert.Equal([client.CreatedId!.Value], client.Deleted), TimeSpan.FromSeconds(3));
    }
}
