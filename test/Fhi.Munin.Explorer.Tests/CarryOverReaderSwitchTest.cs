using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class CarryOverReaderSwitchTest : ExplorerTestContext
{
    private static readonly Texts No = Texts.For("no");

    private sealed class Client : EmptyMuninExplorerClient
    {
        public TaskCompletionSource? Gate { get; set; }
        public bool Reached { get; private set; }
        public int CreateCalls { get; private set; }

        public override async Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            if (Gate is { } gate)
            {
                Gate = null;
                Reached = true;
                await gate.Task;
            }

            return [];
        }

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new VariableList { Id = Guid.NewGuid(), Name = name });
        }
    }

    [Fact]
    public async Task VariableListView_WhenTheReaderChangesDuringTheCarryOversFirstRead_ThenNoListIsMadeAndTheRowsStay()
    {
        // The press belongs to the reader who made it; the next reader's browser rows must not move into a list of theirs.
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
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Gate = gate;

        cut.FindAll("button").Single(b => b.TextContent.Trim() == No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.True(client.Reached));
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        gate.SetResult();
        await cut.InvokeAsync(() => Task.Delay(300));

        Assert.Equal(0, client.CreateCalls);
        Assert.Single(browser.Rows);
    }
}
