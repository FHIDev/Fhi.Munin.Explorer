using Bunit;
using Fhi.Munin.Explorer.Blazor;

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
}
