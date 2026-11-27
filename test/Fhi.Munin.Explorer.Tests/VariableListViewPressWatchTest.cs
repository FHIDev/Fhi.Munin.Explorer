using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

// A host that serves no module answers for good; the signed-in list's watches must not import again every render (Fhi.Metadata-emsc0.3.27).
public class VariableListViewPressWatchTest : ExplorerTestContext
{
    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");

    private sealed class OneListClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = ListId, Name = "Mine hjertevariabler", VariableCount = 0 }]);
    }

    [Fact]
    public void VariableListView_WhenTheHostKeepsRefusingTheModule_ThenEachWatchStopsAskingAfterItsLastTry()
    {
        var (cut, runtime) = RenderSignedIn(new JSException("404"));

        for (var render = 0; render < ExplorerInterop.RefusalsBeforeGivingUp + 2; render++)
        {
            cut.Render();
        }

        // The press watch and the fold watch import apart, so each may be refused this often.
        Assert.Equal(2 * ExplorerInterop.RefusalsBeforeGivingUp, runtime.Imports);
    }

    [Fact]
    public void VariableListView_WhenTheImportFailedTheWayAReconnectDoes_ThenALaterRenderOfTheListsWatchesAsksAgain()
    {
        var (cut, runtime) = RenderSignedIn(new JSDisconnectedException("the circuit is reconnecting"));
        var asked = runtime.Imports;

        cut.Render();

        cut.WaitForAssertion(() => Assert.True(runtime.Imports > asked));
    }

    private (IRenderedComponent<VariableListView> Cut, RefusingJsRuntime Runtime) RenderSignedIn(Exception thrown)
    {
        var runtime = new RefusingJsRuntime(thrown);
        Services.AddSingleton<IJSRuntime>(runtime);
        Services.AddSingleton<IMuninExplorerClient>(new OneListClient());
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForAssertion(() => Assert.Contains("Mine hjertevariabler", cut.Markup, StringComparison.Ordinal));
        cut.WaitForAssertion(() => Assert.True(runtime.Imports > 0));

        return (cut, runtime);
    }
}
