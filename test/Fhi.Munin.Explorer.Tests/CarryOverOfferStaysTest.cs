using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class CarryOverOfferStaysTest : ExplorerTestContext
{
    private static readonly Texts No = Texts.For("no");

    private sealed class Client : EmptyMuninExplorerClient
    {
        public bool ListsThrow { get; set; }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            ListsThrow ? throw new MuninExplorerRateLimitedException() : Task.FromResult<IReadOnlyList<VariableList>>([]);
    }

    [Fact]
    public async Task VariableListView_WhenARefreshOfTheListsFailsAfterTheOfferShowed_ThenTheOfferStaysWhileRowsRemain()
    {
        // The browser rows are still the reader's after a failed re-read, and nothing else on screen gets the button back.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped(_ =>
        {
            var s = new VariableListState(client);
            s.OfferBrowserList(browser);
            return s;
        });
        Services.AddSingleton(new CodePickerSwitch(true));
        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == No.CarryBrowserListOver));
        var state = Services.GetRequiredService<VariableListState>();

        client.ListsThrow = true;
        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => cut.InvokeAsync(() => state.RefreshAsync()));

        await cut.InvokeAsync(() => Task.Delay(100));
        Assert.Single(browser.Rows);
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == No.CarryBrowserListOver);
    }
}
