// Held out: written from the behaviour, not the code (Fhi.Metadata-emsc0.3.18).
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Deleting these leaves the share's answer free to pull focus away from a control the reader moved to while it was out.
/// </summary>
public class AnonymousListShareFocusHeldOutTest : ExplorerTestContext
{
    private const string Focus = "Blazor._internal.domWrapper.focus";

    private static readonly Texts No = Texts.For("no");

    private sealed class Client : EmptyMuninExplorerClient
    {
        public List<(string Name, IReadOnlyCollection<VariableListItem> Items)> Shared { get; } = [];
        public Task<string>? ShareAnswer { get; init; }

        public override Task<string> ShareListAsync(
            string name, IReadOnlyCollection<VariableListItem> items, CancellationToken cancellationToken = default)
        {
            Shared.Add((name, items));
            return ShareAnswer ?? Task.FromResult("ABC123");
        }
    }

    private (IRenderedComponent<VariableListView> Cut, Client Client, TaskCompletionSource<string> Share) Start(int rows)
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Enumerable.Range(0, rows).Select(n => FakeBrowserList.Row($"Variabel {n:000}")));
        var share = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { ShareAnswer = share.Task };
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped(_ =>
        {
            var state = new VariableListState(client);
            state.OfferBrowserList(browser);
            return state;
        });
        Services.AddSingleton(new AnonymousListsSwitch(true));
        return (Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, false)), client, share);
    }

    private static AngleSharp.Dom.IElement ButtonSaying(IRenderedComponent<VariableListView> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private async Task AssertFocusStaysAsync(int rows, Action<IRenderedComponent<VariableListView>> act, Action<IRenderedComponent<VariableListView>>? after = null)
    {
        var (cut, client, share) = Start(rows);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("table.munin-explorer-data-list tbody th[scope=row]")));

        ButtonSaying(cut, No.ShareList).Click();
        cut.WaitForAssertion(() => Assert.Single(client.Shared));
        act(cut);
        var focusCalls = JSInterop.Invocations[Focus].Count;

        await cut.InvokeAsync(() => share.SetResult("ABC123"));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("input[id^=munin-explorer-share-code]")));
        Assert.Equal(focusCalls, JSInterop.Invocations[Focus].Count);
        after?.Invoke(cut);
    }

    [Fact]
    public Task VariableListView_WhenTheReaderPressesClearWhileTheShareIsOut_ThenFocusStaysWhereTheyPutIt() =>
        AssertFocusStaysAsync(1,
            cut =>
            {
                ButtonSaying(cut, No.ClearBrowserList).Click();
                Assert.Contains(No.ConfirmClearBrowserList, cut.Markup);
            },
            cut => Assert.Contains(No.ConfirmClearBrowserList, cut.Markup));

    [Fact]
    public Task VariableListView_WhenTheReaderClosesTheDownloadMenuWithEscapeWhileTheShareIsOut_ThenFocusStaysWhereTheyPutIt() =>
        AssertFocusStaysAsync(1, cut =>
        {
            var toggle = cut.Find("button[id^=munin-explorer-list-download-toggle]");
            toggle.Click();
            Assert.Equal("true", cut.Find("button[id^=munin-explorer-list-download-toggle]").GetAttribute("aria-expanded"));
            cut.Find("button[id^=munin-explorer-list-download-toggle]").ParentElement!
                .KeyDown(new KeyboardEventArgs { Key = "Escape" });
            Assert.Equal("false", cut.Find("button[id^=munin-explorer-list-download-toggle]").GetAttribute("aria-expanded"));
        });

    [Fact]
    public Task VariableListView_WhenTheReaderMovesToAnotherPageWhileTheShareIsOut_ThenFocusStaysWhereTheyPutIt() =>
        AssertFocusStaysAsync(40, cut =>
        {
            cut.Find($"button[aria-label='{No.NextLabel}']").Click();
            cut.WaitForAssertion(() => Assert.Equal("Variabel 025",
                cut.FindAll("table.munin-explorer-data-list tbody th[scope=row] .munin-explorer-dataitem-main__column__text")[0].TextContent.Trim()));
        });
}
