using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

// The press watch imports first and the fold watch second, so the fold's own failed import is the second.
// One failed fetch at circuit start must not switch the list's watches off for the rest of the page.
public class VariableListViewWatchAfterFailedImportHeldOutTest : ExplorerTestContext
{
    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");

    private sealed class OneListClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = ListId, Name = "Mine hjertevariabler", VariableCount = 0 }]);
    }

    private sealed class FailsFirstJsRuntime(IJSObjectReference module, int failures) : IJSRuntime
    {
        private int _imports;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != "import")
            {
                return ValueTask.FromResult(default(TValue)!);
            }

            return Interlocked.Increment(ref _imports) <= failures
                ? throw new JSException("Failed to fetch dynamically imported module")
                : ValueTask.FromResult((TValue)(object)module);
        }
    }

    [Fact]
    public void VariableListView_WhenTheFirstImportFailedOnce_ThenALaterRenderWatchesPresses() =>
        AssertWatchedAfterFailedImport("watchPresses", 1);

    [Fact]
    public void VariableListView_WhenEachWatchsFirstImportFailedOnce_ThenALaterRenderWatchesFolds() =>
        AssertWatchedAfterFailedImport("watchFolds", 2);

    private void AssertWatchedAfterFailedImport(string export, int failures)
    {
        var module = new RecordingModule();
        Services.AddSingleton<IJSRuntime>(new FailsFirstJsRuntime(module, failures));
        Services.AddSingleton<IMuninExplorerClient>(new OneListClient());
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForAssertion(() => Assert.Contains("Mine hjertevariabler", cut.Markup, StringComparison.Ordinal));

        for (var i = 0; i < 3; i++)
        {
            cut.Render();
        }

        cut.WaitForAssertion(() => Assert.Contains(export, module.Calls.Select(c => c.Identifier)));
    }
}
