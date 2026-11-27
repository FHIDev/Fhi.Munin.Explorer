using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Tests;

// A signed-in reader's «Fjern» asks the browser to move focus only if the reader's place left with the row
// (Fhi.Metadata-emsc0.3.24); focusIfLost itself is pinned in scripts/test-focus-if-lost.mjs.
public partial class VariableListViewTest
{
    private const string FocusIfLost = "focusIfLost";

    private static readonly string[] ThreeRows = ["Alder ved diagnose", "Kjønn", "Bosted"];

    private (IRenderedComponent<VariableListView> Cut, BunitJSModuleInterop Module) RenderWithModule(ListClient client, bool focusMoves = true)
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<bool>(FocusIfLost, _ => true).SetResult(focusMoves);
        return (RenderView(client), module);
    }

    private static ListClient Rows(params string[] names) =>
        new([.. names.Select((n, i) => Item(n, $"V_{i}"))]);

    private static List<AngleSharp.Dom.IElement> RemoveButtons(IRenderedComponent<VariableListView> cut) =>
        [.. cut.FindAll("tbody tr td:last-child button")];

    private int ElementFocuses() =>
        JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus");

    [Fact]
    public async Task View_WhenAMiddleRowIsRemoved_ThenTheBrowserIsAskedToFocusTheNextRowsFjernAndNothingDirectly()
    {
        var (cut, module) = RenderWithModule(Rows(ThreeRows));
        var next = RemoveButtons(cut)[2].Id;
        var before = ElementFocuses();

        await cut.InvokeAsync(() => RemoveButtons(cut)[1].Click());

        cut.WaitForAssertion(() => Assert.Equal(next, Assert.Single(module.Invocations[FocusIfLost]).Arguments[0]));
        Assert.Equal(before, ElementFocuses());
    }

    [Fact]
    public async Task View_WhenFocusIfLostFaultsInTheModule_ThenItIsLoggedAsAnErrorAndNothingElseTakesFocus()
    {
        // A fault inside the module is a defect to see, not a host without the module: no .NET focus stands in for it.
        var recorder = new RecordingLoggerProvider();
        Services.AddLogging(b => b.AddProvider(recorder).SetMinimumLevel(LogLevel.Trace));
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        var fault = new Microsoft.JSInterop.JSException("focusIfLost is not a function");
        module.Setup<bool>(FocusIfLost, _ => true).SetException(fault);
        var cut = RenderView(Rows(ThreeRows));
        var before = ElementFocuses();

        await cut.InvokeAsync(() => RemoveButtons(cut)[1].Click());

        cut.WaitForAssertion(() => Assert.Single(module.Invocations[FocusIfLost]));
        var entry = Assert.Single(recorder.Entries, e => e.Category == typeof(VariableListView).FullName && e.Exception == fault);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(before, ElementFocuses());
    }

    [Fact]
    public async Task View_WhenTheLastRowIsRemoved_ThenTheBrowserIsAskedToFocusTheRowBefore()
    {
        var (cut, module) = RenderWithModule(Rows(ThreeRows));
        var previous = RemoveButtons(cut)[1].Id;

        await cut.InvokeAsync(() => RemoveButtons(cut)[2].Click());

        cut.WaitForAssertion(() => Assert.Equal(previous, Assert.Single(module.Invocations[FocusIfLost]).Arguments[0]));
    }

    [Fact]
    public async Task View_WhenTheOnlyRowIsRemoved_ThenTheBrowserIsAskedToFocusTheListMenu()
    {
        var (cut, module) = RenderWithModule(Rows("Alder ved diagnose"));
        var menu = cut.Find("button[id^='munin-explorer-list-menu-toggle-']").Id;

        await cut.InvokeAsync(() => RemoveButtons(cut)[0].Click());

        cut.WaitForAssertion(() => Assert.Equal(menu, Assert.Single(module.Invocations[FocusIfLost]).Arguments[0]));
    }

    [Fact]
    public async Task View_WhenARowIsRemovedAndTheReaderMovedOn_ThenNothingElseTakesFocusAndTheRemovalIsSaid()
    {
        // A reader who Tabbed or clicked away during the call: focusIfLost answers false, and no fallback steals focus.
        var (cut, module) = RenderWithModule(Rows(ThreeRows), focusMoves: false);
        var before = ElementFocuses();

        await cut.InvokeAsync(() => RemoveButtons(cut)[1].Click());

        cut.WaitForAssertion(() => Assert.Single(module.Invocations[FocusIfLost]));
        for (var i = 0; i < 3; i++)
        {
            await cut.InvokeAsync(() => { });
        }

        Assert.Equal(before, ElementFocuses());
        Assert.Single(module.Invocations[FocusIfLost]);
        Assert.Contains("er fjernet fra listen", cut.Find(".screenreader-only[role=status]").TextContent, StringComparison.Ordinal);
    }

    // A page read from before the removal may land after it, still holding the row: its button still has focus then.
    private sealed class StaleReadClient(params string[] names) : EmptyMuninExplorerClient
    {
        private static readonly Guid ListId = new("22222222-2222-2222-2222-222222222222");
        private readonly List<VariableListItem> _items = [.. names.Select((n, i) => Item(n, $"V_{i}"))];
        private List<VariableListItem>? _before;

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = ListId, Name = "Mine", VariableCount = _items.Count }]);

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            _before = [.. _items];
            _items.RemoveAll(i => variableIds.Contains(i.VariableId));
            return Task.FromResult(true);
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            // The first read after the removal answers with the rows from before it.
            var rows = _before ?? _items;
            _before = null;
            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. rows],
                TotalCount = rows.Count,
                PageNumber = page,
                Size = pageSize,
                TotalPages = 1,
            });
        }
    }

    [Fact]
    public async Task View_WhenAReadStillShowingTheRemovedRowLandsFirst_ThenFocusIsAskedOnlyOnceTheRowHasGone()
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<bool>(FocusIfLost, _ => true).SetResult(true);
        Services.AddSingleton(new CodePickerSwitch(true));
        Services.AddSingleton<IMuninExplorerClient>(new StaleReadClient(ThreeRows));
        Services.AddScoped<VariableListState>();
        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForAssertion(() => Assert.Equal(3, RemoveButtons(cut).Count));
        var next = RemoveButtons(cut)[2].Id;

        await cut.InvokeAsync(() => RemoveButtons(cut)[1].Click());
        for (var i = 0; i < 3; i++)
        {
            await cut.InvokeAsync(() => { });
        }

        Assert.Equal(3, RemoveButtons(cut).Count);
        Assert.Empty(module.Invocations[FocusIfLost]);

        await cut.InvokeAsync(() => Services.GetRequiredService<VariableListState>().RefreshAsync());

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        cut.WaitForAssertion(() => Assert.Equal(next, Assert.Single(module.Invocations[FocusIfLost]).Arguments[0]));
    }
}
