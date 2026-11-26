using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class AnonymousListRemoveRowHeldOut2Test : ExplorerTestContext
{
    private const string Focus = "focusIfLost";

    private static readonly Texts No = Texts.For("no");

    private sealed class Client : EmptyMuninExplorerClient;

    private IRenderedComponent<VariableListView> ListView(FakeBrowserList browser)
    {
        var client = new Client();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped(_ =>
        {
            var state = new VariableListState(client);
            state.OfferBrowserList(browser);
            return state;
        });
        Services.AddSingleton(new CodePickerSwitch(true));

        return Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, false));
    }

    private static IElement RemoveButtonOf(IRenderedComponent<VariableListView> cut, string name) =>
        cut.FindAll("table.munin-explorer-data-list tbody tr")
            .Single(r => r.QuerySelector("th[scope=row] .munin-explorer-dataitem-main__column__text")!.TextContent.Trim() == name)
            .QuerySelector("button")!;

    // Moving focus off the open question would let the next Enter remove another row.
    // The focus step runs one render after the rows show, so the test lets it run before asserting.
    [Fact]
    public async Task VariableListView_WhenTheQuestionOpensWhileARemovalIsOut_ThenTheRemovalLandsWithoutMovingFocus()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(new[] { "Alder", "Kjønn", "Vekt" }.Select(n => FakeBrowserList.Row(n)));
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table.munin-explorer-data-list tbody tr").Count));
        browser.Hold = true;

        await cut.InvokeAsync(() => RemoveButtonOf(cut, "Kjønn").Click());
        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == No.ClearBrowserList).Click());

        browser.Hold = false;
        while (browser.Pending.Count > 0)
        {
            await cut.InvokeAsync(browser.ReleaseNewest);
        }

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("table.munin-explorer-data-list tbody tr").Count));
        await cut.InvokeAsync(() => cut.Render());
        await cut.InvokeAsync(() => { });
        Assert.Empty(JSInterop.Invocations[Focus]);
    }
}
