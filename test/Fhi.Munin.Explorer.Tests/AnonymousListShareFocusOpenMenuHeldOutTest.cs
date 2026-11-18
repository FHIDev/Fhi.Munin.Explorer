// Held out: written from the behaviour, not the code (Fhi.Metadata-emsc0.3.18).
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Deleting these leaves a share's answer free to pull focus to the code after the reader worked the download menu that was already open.
/// </summary>
public class AnonymousListShareFocusOpenMenuHeldOutTest : ExplorerTestContext
{
    private const string Focus = "Blazor._internal.domWrapper.focus";
    private const string ToggleSelector = "button[id^=munin-explorer-list-download-toggle]";

    private static readonly Texts No = Texts.For("no");

    private sealed class Client : EmptyMuninExplorerClient
    {
        public List<(string Name, IReadOnlyCollection<VariableListItem> Items)> Shared { get; } = [];
        public List<IReadOnlyCollection<VariableDatasamlingKey>> Exported { get; } = [];
        public Task<string>? ShareAnswer { get; init; }

        public override Task<string> ShareListAsync(
            string name, IReadOnlyCollection<VariableListItem> items, CancellationToken cancellationToken = default)
        {
            Shared.Add((name, items));
            return ShareAnswer ?? Task.FromResult("ABC123");
        }

        public override Task<ExportedList> ExportItemsAsync(
            IReadOnlyCollection<VariableDatasamlingKey> items, Uri? explorerPageUrl = null,
            ExportFormat format = ExportFormat.Xlsx, bool includeKodeverk = false, CancellationToken cancellationToken = default)
        {
            Exported.Add(items);
            return Task.FromResult(new ExportedList([1], "application/octet-stream", "Variabelliste.xlsx"));
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
        Services.AddSingleton(new CodePickerSwitch(true));
        return (Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, false)), client, share);
    }

    private static AngleSharp.Dom.IElement ButtonSaying(IRenderedComponent<VariableListView> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private async Task AssertFocusStaysAsync(Action<IRenderedComponent<VariableListView>, Client> act)
    {
        var (cut, client, share) = Start();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("table.munin-explorer-data-list tbody th[scope=row]")));

        cut.Find(ToggleSelector).Click();
        Assert.Equal("true", cut.Find(ToggleSelector).GetAttribute("aria-expanded"));

        ButtonSaying(cut, No.ShareList).Click();
        cut.WaitForAssertion(() => Assert.Single(client.Shared));
        Assert.Equal("true", cut.Find(ToggleSelector).GetAttribute("aria-expanded"));

        act(cut, client);
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

    [Fact]
    public Task VariableListView_WhenTheOpenDownloadMenuIsClosedWithEscapeWhileTheShareIsOut_ThenFocusIsNotMovedToTheCode() =>
        AssertFocusStaysAsync((cut, _) =>
        {
            cut.Find(ToggleSelector).ParentElement!.KeyDown(new KeyboardEventArgs { Key = "Escape" });
            Assert.Equal("false", cut.Find(ToggleSelector).GetAttribute("aria-expanded"));
        });

    [Fact]
    public Task VariableListView_WhenADownloadFormatIsPressedInTheOpenMenuWhileTheShareIsOut_ThenFocusIsNotMovedToTheCode() =>
        AssertFocusStaysAsync((cut, client) =>
        {
            cut.FindAll("[id^=munin-explorer-list-download-] button.munin-explorer-list-menu__item")[0].Click();
            cut.WaitForAssertion(() => Assert.Single(client.Exported));
        });

    [Fact]
    public Task VariableListView_WhenTheKodeverkCheckboxIsToggledInTheOpenMenuWhileTheShareIsOut_ThenFocusIsNotMovedToTheCode() =>
        AssertFocusStaysAsync((cut, _) =>
        {
            var box = cut.Find("[id^=munin-explorer-list-download-] input[type=checkbox]");
            box.Change(true);
            Assert.True(cut.Find("[id^=munin-explorer-list-download-] input[type=checkbox]").HasAttribute("checked"));
        });
}
