using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// <see cref="VariableSearch.OwnListId"/> mounted without <see cref="VariableExplorer"/> (Fhi.Metadata-9huq2.61): the
/// explorer drops the id itself with the flag off, so only a host mounting the search directly sees the search's own gate.
/// </summary>
public class VariableSearchOwnListLinkTest : ExplorerTestContext
{
    private sealed class NoCallsClient : EmptyMuninExplorerClient;

    private IRenderedComponent<VariableSearch> RenderSearch(bool flag)
    {
        Services.AddSingleton<IMuninExplorerClient>(new NoCallsClient());
        Services.AddSingleton(new ListLinkSwitch(flag));
        Services.AddScoped<VariableListState>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        return Render<VariableSearch>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.OwnListId, Guid.NewGuid())
            .Add(c => c.VariableList, builder => builder.AddContent(0, "the lists")));
    }

    private static bool ListTabSelected(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").GetAttribute("aria-selected") == "true";

    [Fact]
    public void OwnListId_WhenTheFlagIsOff_ThenTheListTabDoesNotOpen()
    {
        var cut = RenderSearch(flag: false);

        Assert.False(ListTabSelected(cut));
    }

    [Fact]
    public void OwnListId_WhenTheFlagIsOn_ThenTheListTabOpens()
    {
        var cut = RenderSearch(flag: true);

        cut.WaitForAssertion(() => Assert.True(ListTabSelected(cut)));
    }
}
