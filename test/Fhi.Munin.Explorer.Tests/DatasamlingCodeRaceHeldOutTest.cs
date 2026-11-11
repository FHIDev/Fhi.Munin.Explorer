using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class DatasamlingCodeRaceHeldOutTest : ExplorerTestContext
{
    [Fact]
    public void DatasamlingCode_WhenTwoArrivalsAtOneAddressAndTheOlderLookupLandsFirst_ThenTheDatasamlingStillOpens()
    {
        // The stale kilde lookup's chain must not clear the newer arrival's pending datasamling code.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();
        TaskCompletionSource<Guid?> first = new(), second = new();
        var client = new RaceClient(kilde, datasamling, new([first, second]));
        Services.AddSingleton<IMuninExplorerClient>(client);
        SetRendererInfo(new RendererInfo("Server", true));
        const string url = "http://localhost/kilder?kilde=als&datasamling=K_ALS.INKLUSJON";
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);

        var cut = Render<KildeExplorer>();
        Services.GetRequiredService<NavigationManager>().NavigateTo(url, forceLoad: false, replace: true);

        cut.InvokeAsync(() => first.SetResult(kilde));
        cut.InvokeAsync(() => second.SetResult(kilde));

        cut.WaitForAssertion(() => Assert.Equal(kilde, client.LastDetailId));
        cut.WaitForAssertion(() =>
            Assert.Equal(datasamling, cut.FindComponent<KildeSearch>().Instance.SelectedDatasamlingId));
    }

    private sealed class RaceClient(Guid kilde, Guid datasamling, Queue<TaskCompletionSource<Guid?>> lookups)
        : EmptyMuninExplorerClient
    {
        public Guid? LastDetailId { get; private set; }

        public override Task<Guid?> FindKildeIdByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            lookups.Dequeue().Task;

        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>([]);

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            LastDetailId = id;
            return Task.FromResult<KildeDetail?>(new KildeDetail
            {
                Id = id,
                Code = "K_ALS",
                PreferredTerm = "Als registeret",
                Datasamlinger = [new() { Id = datasamling, Code = "K_ALS.INKLUSJON", Name = "Inklusjon", VariableCount = 12 }],
            });
        }

        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(new KildeHierarchy
            {
                KildeId = id,
                DirectDatasamlinger = [new() { Id = datasamling, Name = "Inklusjon" }],
            });

        public override Task<DatasamlingDetail?> GetDatasamlingAsync(Guid collectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DatasamlingDetail?>(new()
            {
                Id = collectionId,
                Code = "K_ALS.INKLUSJON",
                PreferredTerm = "Inklusjon",
                ParentKildeName = "Als registeret",
                ParentKildeId = kilde,
                VariableCount = 12,
            });
    }
}
