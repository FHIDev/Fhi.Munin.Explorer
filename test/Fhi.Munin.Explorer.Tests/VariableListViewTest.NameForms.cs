using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The rename form closes the way the create form does, and Enter in a name field submits it
/// (Fhi.Metadata-mhqx0.16).
/// </summary>
public partial class VariableListViewTest
{
    private const string RenameInput = "input[id^='munin-explorer-rename-list-']";

    private static AngleSharp.Dom.IElement RenameForm(IRenderedComponent<VariableListView> cut) =>
        cut.Find(RenameInput).Closest("form")!;

    [Fact]
    public async Task Rename_WhenCancelIsPressed_ThenTheFormClosesAndFocusGoesToTheMenuToggle()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));
        var cut = RenderView(client);
        RenameField(cut).Change("Hjertet mitt");

        await cut.InvokeAsync(() => RenameForm(cut).QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Avbryt").Click());

        Assert.Empty(cut.FindAll(RenameInput));
        Assert.Equal(0, client.RenameCalls);
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_menuToggle"), FocusedId()));
    }

    [Fact]
    public async Task Rename_WhenEscapeIsPressedInTheField_ThenTheFormClosesAndFocusGoesToTheMenuToggle()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));
        var cut = RenderView(client);
        var field = RenameField(cut);

        await cut.InvokeAsync(() => field.KeyDown(new KeyboardEventArgs { Key = "Escape" }));

        Assert.Empty(cut.FindAll(RenameInput));
        Assert.Equal(0, client.RenameCalls);
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_menuToggle"), FocusedId()));
    }

    [Fact]
    public async Task Rename_WhenReopenedAfterEscape_ThenTheFieldIsEmpty()
    {
        // A cancelled name left in the field would be saved by the next Enter without the reader retyping it.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        RenameField(cut).Change("Hjertet mitt");
        await cut.InvokeAsync(() => RenameForm(cut).KeyDown(new KeyboardEventArgs { Key = "Escape" }));

        Assert.Equal("", RenameField(cut).GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task Rename_WhenEnterSubmitsTwiceWhileTheCallIsOut_ThenItRenamesOnceAndCannotBeShut()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { DuringRename = () => gate.Task };
        var cut = RenderView(client);
        RenameField(cut).Change("Hjertet mitt");

        var first = cut.InvokeAsync(() => RenameForm(cut).Submit());
        await cut.WaitForAssertionAsync(() => Assert.True(cut.Find(RenameInput).HasAttribute("readonly")));
        await cut.InvokeAsync(() => RenameForm(cut).Submit());
        await cut.InvokeAsync(() => RenameForm(cut).KeyDown(new KeyboardEventArgs { Key = "Escape" }));

        Assert.Single(cut.FindAll(RenameInput));
        await cut.InvokeAsync(gate.SetResult);
        await first;

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(RenameInput)));
        Assert.Equal(1, client.RenameCalls);
        Assert.Equal("Hjertet mitt", client.LastRenamedTo);
    }

    [Fact]
    public async Task Rename_WhenTheMenuShutsTheForm_ThenTheFieldIsEmptyWhenItOpensAgain()
    {
        // Now that Enter saves, a name shut away and shown again would be one key from being saved unseen.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        RenameField(cut).Change("Hjertet mitt");
        await cut.InvokeAsync(() => cut.Find("button[id^='munin-explorer-rename-toggle-']").Click());

        Assert.Empty(cut.FindAll(RenameInput));
        Assert.Equal("", RenameField(cut).GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task Rename_WhileTheCallIsOut_ThenTheMenuItemIsRefusedAndTheButtonSaysSo()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { DuringRename = () => gate.Task };
        var cut = RenderView(client);
        RenameField(cut).Change("Hjertet mitt");

        var first = cut.InvokeAsync(() => RenameForm(cut).Submit());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Lagrer …", RenameForm(cut).QuerySelector("button[type='submit']")!.TextContent.Trim()));
        var toggle = cut.Find("button[id^='munin-explorer-rename-toggle-']");
        Assert.Equal("true", toggle.GetAttribute("aria-disabled"));
        await cut.InvokeAsync(() => toggle.Click());

        Assert.Single(cut.FindAll(RenameInput));
        await cut.InvokeAsync(gate.SetResult);
        await first;
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(RenameInput)));
        Assert.Equal(1, client.RenameCalls);
        Assert.Null(cut.Find("button[id^='munin-explorer-rename-toggle-']").GetAttribute("aria-disabled"));
    }

    [Fact]
    public async Task Create_WhenEnterSubmitsTwiceWhileTheCallIsOut_ThenOneListIsCreated()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { DuringCreate = () => gate.Task };
        var cut = RenderView(client);
        CreateField(cut).Change("Kreft og svulster");

        var first = cut.InvokeAsync(() => cut.Find(CreateForm).Submit());
        await cut.WaitForAssertionAsync(() => Assert.Equal("true", Submit(cut)!.GetAttribute("aria-busy")));
        await cut.InvokeAsync(() => cut.Find(CreateForm).Submit());
        await cut.InvokeAsync(gate.SetResult);
        await first;

        Assert.Equal(1, client.CreateCalls);
    }

    [Fact]
    public void NameFields_WhenTheirFormsAreOpen_ThenEachSitsInAFormWhoseOneSubmitIsItsAction()
    {
        // Enter submits a form, and only a type="submit" button is pressed by it; a type="button" here
        // would leave Enter submitting while the visible action stayed a plain button.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var create = CreateField(cut).Closest("form")!;
        var rename = RenameField(cut).Closest("form")!;

        Assert.Equal(["Opprett liste"], create.QuerySelectorAll("button[type='submit']").Select(b => b.TextContent.Trim()));
        Assert.Equal(["Lagre navnet"], rename.QuerySelectorAll("button[type='submit']").Select(b => b.TextContent.Trim()));
        Assert.Equal("button", rename.QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Avbryt").GetAttribute("type"));
    }
}
