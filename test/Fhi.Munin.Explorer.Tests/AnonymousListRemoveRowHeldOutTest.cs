using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Held-out cases for the browser list's «Fjern» (Fhi.Metadata-emsc0.3.13): a repeat press and a new reader mid-removal.
/// </summary>
public class AnonymousListRemoveRowHeldOutTest : ExplorerTestContext
{
    private const string Focus = "Blazor._internal.domWrapper.focus";

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

        return Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, false).Add(c => c.PageSize, 25));
    }

    private static List<string> RowNames(IRenderedComponent<VariableListView> cut) =>
        [.. cut.FindAll("table.munin-explorer-data-list tbody th[scope=row] .munin-explorer-dataitem-main__column__text").Select(c => c.TextContent.Trim())];

    private static IElement RemoveButtonOf(IRenderedComponent<VariableListView> cut, string name) =>
        cut.FindAll("table.munin-explorer-data-list tbody tr")
            .Single(r => r.QuerySelector("th[scope=row] .munin-explorer-dataitem-main__column__text")!.TextContent.Trim() == name)
            .QuerySelector("button")!;

    private static FakeBrowserList Holding(params string[] names)
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(names.Select(n => FakeBrowserList.Row(n)));
        return browser;
    }

    private static async Task Release(IRenderedComponent<VariableListView> cut, FakeBrowserList browser, string call)
    {
        var at = browser.Pending.FindIndex(p => p.Call == call);
        Assert.True(at >= 0, $"no held {call} call");
        var release = browser.Pending[at].Release;
        browser.Pending.RemoveAt(at);
        await cut.InvokeAsync(() => release.SetResult());
    }

    [Fact]
    public async Task VariableListView_WhenRemoveIsPressedAgainAfterTheBrowserAnsweredButBeforeThePageIsReadBack_ThenNothingHappens()
    {
        // The row stays drawn until the page is read back, so a press there is a real one that must not remove twice.
        var browser = Holding("Alder", "Kjønn");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        browser.Hold = true;

        var button = RemoveButtonOf(cut, "Alder");
        var first = cut.InvokeAsync(() => button.Click());
        cut.WaitForAssertion(() => Assert.Contains(browser.Pending, p => p.Call == "remove"));
        await Release(cut, browser, "remove");
        await first;
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));

        // The page read for the change is held, so the removed row is still on screen.
        cut.WaitForAssertion(() => Assert.Contains(browser.Pending, p => p.Call == "page"));
        Assert.Contains("Alder", RowNames(cut));
        Assert.Equal(No.RemovedFromList("Alder"), cut.Find("[role=status]").TextContent.Trim());

        var again = RemoveButtonOf(cut, "Alder");
        await cut.InvokeAsync(() => again.Click());

        // A let-through press waits its turn behind the held page read, so everything held is let go before looking.
        browser.Hold = false;
        for (var turn = 0; turn < 10; turn++)
        {
            while (browser.Pending.Count > 0)
            {
                await cut.InvokeAsync(browser.ReleaseNewest);
            }

            await Task.Delay(50);
        }

        cut.WaitForAssertion(() => Assert.DoesNotContain("Alder", RowNames(cut)));
        Assert.Single(browser.Calls, c => c.Call == "remove");
        Assert.Equal(No.RemovedFromList("Alder"), cut.Find("[role=status]").TextContent.Trim());
    }

    [Fact]
    public async Task VariableListView_WhenTheReaderSignsInAndOutWhileARemovalIsOut_ThenTheRemovalMovesNoFocus()
    {
        // A sign-in is a new reader at the screen; the earlier reader's removal has no say over where focus goes.
        var browser = Holding("Alder", "Kjønn", "Vekt");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        browser.Hold = true;

        var button = RemoveButtonOf(cut, "Kjønn");
        var first = cut.InvokeAsync(() => button.Click());
        cut.WaitForAssertion(() => Assert.Contains(browser.Pending, p => p.Call == "remove"));

        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));

        browser.Hold = false;
        while (browser.Pending.Count > 0)
        {
            await cut.InvokeAsync(browser.ReleaseNewest);
        }

        await first;
        cut.WaitForAssertion(() => Assert.DoesNotContain("Kjønn", RowNames(cut)));

        // The focus step runs one render after the rows appear: give it that render and a turn before asserting.
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        await cut.InvokeAsync(() => Task.CompletedTask);

        Assert.Empty(JSInterop.Invocations[Focus]);
    }
}
