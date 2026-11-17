// Held out: written from the behaviour, not the code (Fhi.Metadata-emsc0.3.18).
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Deleting these leaves a share's answer free to pull focus to the code after the reader ticked a column in the picker that was already open.
/// </summary>
public class AnonymousListShareFocusColumnsHeldOutTest : ExplorerTestContext
{
    private const string Focus = "Blazor._internal.domWrapper.focus";
    private const string ToggleSelector = "button[id^=munin-explorer-columns-toggle]";

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

    private (IRenderedComponent<VariableListView> Cut, Client Client, TaskCompletionSource<string> Share) Start()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Enumerable.Range(0, 1).Select(n => FakeBrowserList.Row($"Variabel {n:000}")));
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

    [Fact]
    public async Task VariableListView_WhenAColumnIsTickedInTheOpenColumnPickerWhileTheShareIsOut_ThenFocusIsNotMovedToTheCode()
    {
        var (cut, client, share) = Start();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("table.munin-explorer-data-list tbody th[scope=row]")));

        cut.Find(ToggleSelector).Click();
        Assert.Equal("true", cut.Find(ToggleSelector).GetAttribute("aria-expanded"));

        ButtonSaying(cut, No.ShareList).Click();
        cut.WaitForAssertion(() => Assert.Single(client.Shared));
        Assert.Equal("true", cut.Find(ToggleSelector).GetAttribute("aria-expanded"));

        var box = cut.FindAll("[aria-labelledby^='munin-explorer-columns-toggle-'] input[type=checkbox]")
            .First(b => !b.HasAttribute("checked") && !b.HasAttribute("aria-disabled"));
        box.Change(true);
        var focusCalls = JSInterop.Invocations[Focus].Count;

        await cut.InvokeAsync(() => share.SetResult("ABC123"));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("input[id^=munin-explorer-share-code]")));
        var codeRef = cut.Find("input[id^=munin-explorer-share-code]").GetAttribute("blazor:elementreference");
        var calls = JSInterop.Invocations[Focus].ToList();
        if (calls.Count > 0)
        {
            var last = Assert.IsType<ElementReference>(calls[^1].Arguments[0]);
            Assert.NotEqual(codeRef, last.Id);
        }

        Assert.Equal(focusCalls, calls.Count);
    }
}
