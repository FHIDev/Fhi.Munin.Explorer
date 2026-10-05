using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The action row: the picker, one primary button, the download and a «Flere valg» fold holding the
/// rest, so the row is one line at 1280 and the extras do not stand five rows deep at 320.
/// </summary>
public partial class VariableListViewTest
{
    private const string MenuToggle = "button[id^='munin-explorer-list-menu-toggle-']";
    private const string MenuPanel = "div[id^='munin-explorer-list-menu-']";
    private const string DownloadToggle = "button[id^='munin-explorer-list-download-toggle-']";
    private const string DownloadPanel = "div[id^='munin-explorer-list-download-']";

    private sealed class ShareClient(params VariableListItem[] items) : ListClient(items)
    {
        public override Task<string> ShareListAsync(
            string name, IReadOnlyCollection<VariableListItem> items, CancellationToken cancellationToken = default) =>
            Task.FromResult("ABC123");
    }

    private static string[] Words(IEnumerable<AngleSharp.Dom.IElement> buttons) =>
        [.. buttons.Select(b => b.TextContent.Trim())];

    private static AngleSharp.Dom.IElement Menu(IRenderedComponent<VariableListView> cut) => cut.Find(MenuToggle);

    private static void OpenMenu(IRenderedComponent<VariableListView> cut)
    {
        if (Menu(cut).GetAttribute("aria-expanded") == "false")
        {
            Menu(cut).Click();
        }
    }

    private static Task ChooseAsync(IRenderedComponent<VariableListView> cut, string word) =>
        cut.InvokeAsync(() => cut.FindAll($"{MenuPanel} button").First(b => b.TextContent.Trim() == word).Click());

    [Fact]
    public void ActionRow_WhenAListIsShown_ThenOnlyCreateAndTheTwoFoldsStandOutsideThem()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var loose = cut.FindAll($"{ActionRow} button")
                       .Where(b => b.Closest(DownloadPanel) is null && b.Closest(MenuPanel) is null);

        Assert.Equal(["Legg til ny liste", "Last ned", "Flere valg"], Words(loose));
        Assert.Empty(cut.FindAll($"{ActionRow} ~ * button[id^='munin-explorer-rename-toggle-']"));
    }

    [Fact]
    public void Menu_WhenAListIsShown_ThenItHoldsTheListsActionsAndOpeningASharedOneLast()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        Assert.Equal(
            ["Gi nytt navn", "Del liste", "Kopier liste", "Tøm liste", "Slett listen", "Åpne delt liste"],
            Words(cut.FindAll($"{MenuPanel} button")));
    }

    [Fact]
    public void Menu_WhenNoListIsShown_ThenItHoldsOnlyOpeningASharedList()
    {
        var cut = RenderView(new ListClient { HasList = false });

        Assert.Equal(["Åpne delt liste"], Words(cut.FindAll($"{MenuPanel} button")));
    }

    [Fact]
    public void Menu_WhenClosed_ThenItsPanelIsHiddenAndTheToggleNamesIt()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var panel = cut.Find(MenuPanel);
        Assert.Equal("false", Menu(cut).GetAttribute("aria-expanded"));
        Assert.Equal(panel.Id, Menu(cut).GetAttribute("aria-controls"));
        Assert.True(panel.HasAttribute("hidden"));
    }

    [Fact]
    public void Menu_WhenTheToggleIsPressed_ThenThePanelShows()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        Menu(cut).Click(new MouseEventArgs { Detail = 0 });

        Assert.Equal("true", Menu(cut).GetAttribute("aria-expanded"));
        Assert.False(cut.Find(MenuPanel).HasAttribute("hidden"));
    }

    [Fact]
    public void Menu_WhenEscapeIsPressedInIt_ThenItClosesAndFocusReturnsToTheToggle()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);

        cut.FindAll($"{MenuPanel} button")[1].KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("false", Menu(cut).GetAttribute("aria-expanded"));
        Assert.True(cut.Find(MenuPanel).HasAttribute("hidden"));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_menuToggle"), FocusedId()));
    }

    [Fact]
    public void Menu_WhenAnotherKeyIsPressedInIt_ThenItStaysOpen()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);

        cut.FindAll($"{MenuPanel} button")[1].KeyDown(new KeyboardEventArgs { Key = "Tab" });

        Assert.Equal("true", Menu(cut).GetAttribute("aria-expanded"));
    }

    [Theory]
    [InlineData("Slett listen", "munin-explorer-delete-list-")]
    [InlineData("Tøm liste", "munin-explorer-empty-list-")]
    public async Task Menu_WhenItClosesWithAConfirmationArmed_ThenTheConfirmationIsDisarmed(string word, string stem)
    {
        // An armed delete left behind a closed fold would be one press from destroying the list the
        // next time the reader opened it, with nothing in between saying so.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);
        await ChooseAsync(cut, word);
        Assert.Equal("true", Disclosed(cut, stem));

        Menu(cut).KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("false", Disclosed(cut, stem));
    }

    [Fact]
    public async Task Menu_WhenTheToggleClosesItWithADeleteArmed_ThenTheDeleteIsDisarmed()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);
        await ChooseAsync(cut, "Slett listen");

        Menu(cut).Click();

        Assert.Equal("false", Disclosed(cut, DeleteToggle));
    }

    [Fact]
    public async Task Menu_WhenDeleteIsChosen_ThenItStaysOpenForTheConfirmation()
    {
        // The question and its answer are in the fold beside the control that asked it.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);

        await ChooseAsync(cut, "Slett listen");

        Assert.Equal("true", Menu(cut).GetAttribute("aria-expanded"));
        Assert.Contains(cut.FindAll($"{MenuPanel} button"), b => b.TextContent.Trim() == "Ja, slett listen");
    }

    [Fact]
    public async Task Menu_WhenTheDeleteIsConfirmed_ThenItClosesAndFocusGoesToTheToggle()
    {
        // The pressed button leaves with the list; focus must not drop to <body>.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));
        var cut = RenderView(client);
        OpenMenu(cut);
        await ChooseAsync(cut, "Slett listen");

        await ChooseAsync(cut, "Ja, slett listen");

        Assert.Equal(1, client.DeleteCalls);
        Assert.Equal("false", Menu(cut).GetAttribute("aria-expanded"));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_menuToggle"), FocusedId()));
    }

    [Theory]
    [InlineData("Gi nytt navn", "_renameField")]
    [InlineData("Kopier liste", "_copyField")]
    [InlineData("Åpne delt liste", "_sharedCodeField")]
    public async Task Menu_WhenAFormIsChosen_ThenItClosesAndFocusGoesToTheFormsField(string word, string field)
    {
        // The chosen button leaves with the fold, so focus goes where the reader is to type.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);

        await ChooseAsync(cut, word);

        Assert.Equal("false", Menu(cut).GetAttribute("aria-expanded"));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, field), FocusedId()));
    }

    [Fact]
    public async Task Menu_WhenSharingIsChosen_ThenItClosesAndFocusGoesToTheCode()
    {
        var cut = RenderView(new ShareClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);

        await ChooseAsync(cut, "Del liste");

        cut.WaitForAssertion(() => Assert.Equal("ABC123", cut.Find("input[id^='munin-explorer-share-code-']").GetAttribute("value")));
        Assert.Equal("false", Menu(cut).GetAttribute("aria-expanded"));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_shareCodeField"), FocusedId()));
    }

    [Fact]
    public async Task Menu_WhenAnOpenFormIsChosenAgain_ThenTheFormFoldsAndFocusGoesToTheToggle()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);
        await ChooseAsync(cut, "Gi nytt navn");
        OpenMenu(cut);

        await ChooseAsync(cut, "Gi nytt navn");

        Assert.Empty(cut.FindAll("input[id^='munin-explorer-rename-list-']"));
        Assert.Equal("false", Menu(cut).GetAttribute("aria-expanded"));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_menuToggle"), FocusedId()));
    }

    [Fact]
    public async Task Menu_WhenARefusedActionIsChosen_ThenItStaysOpen()
    {
        // Copying an empty list is refused with its reason beside it; folding the menu would hide both.
        var cut = RenderView(new ListClient());
        OpenMenu(cut);

        await ChooseAsync(cut, "Kopier liste");

        Assert.Equal("true", Menu(cut).GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task Menu_WhenTheEmptyIsConfirmed_ThenItClosesAndFocusGoesToTheToggle()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);
        await ChooseAsync(cut, "Tøm liste");

        await ChooseAsync(cut, "Ja, tøm listen");

        Assert.Equal("false", Menu(cut).GetAttribute("aria-expanded"));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_menuToggle"), FocusedId()));
    }

    [Fact]
    public void Menu_WhenEscapeIsPressedOnTheClosedToggle_ThenFocusIsNotMoved()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        Menu(cut).KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public async Task Menu_WhenSharingFails_ThenFocusGoesToTheToggleRatherThanAMissingCode()
    {
        // The fake refuses to share, so the panel opens with no code field to receive focus.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);

        await ChooseAsync(cut, "Del liste");

        Assert.Empty(cut.FindAll("input[id^='munin-explorer-share-code-']"));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_menuToggle"), FocusedId()));
    }

    [Fact]
    public void Download_WhenClosed_ThenItsPanelIsHiddenAndTheToggleNamesIt()
    {
        // The same fold as «Flere valg», so the row has one way of opening things.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var toggle = cut.Find(DownloadToggle);
        var panel = cut.Find(DownloadPanel);
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Equal(panel.Id, toggle.GetAttribute("aria-controls"));
        Assert.True(panel.HasAttribute("hidden"));
        Assert.Contains("hd-button-square", toggle.ClassList);
    }

    [Fact]
    public void Download_WhenEscapeIsPressedInIt_ThenItClosesAndFocusReturnsToItsToggle()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        cut.Find(DownloadToggle).Click();
        Assert.False(cut.Find(DownloadPanel).HasAttribute("hidden"));

        cut.Find($"{DownloadPanel} button").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.True(cut.Find(DownloadPanel).HasAttribute("hidden"));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_downloadToggle"), FocusedId()));
    }

    [Fact]
    public void Download_WhenAnotherKeyIsPressedInIt_ThenItStaysOpen()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        cut.Find(DownloadToggle).Click();

        cut.Find($"{DownloadPanel} button").KeyDown(new KeyboardEventArgs { Key = "Tab" });

        Assert.False(cut.Find(DownloadPanel).HasAttribute("hidden"));
    }

    [Fact]
    public void Download_WhenEscapeIsPressedOnTheClosedToggle_ThenFocusIsNotMoved()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        cut.Find(DownloadToggle).KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public void Folds_WhenDrawn_ThenOnlyFlereValgIsPushedToTheEndOfItsLine()
    {
        // The download's panel opens rightward from the start of the line, «Flere valg»'s leftward from its end.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        Assert.Contains("munin-explorer-list-menu--end", cut.Find(MenuToggle).ParentElement!.ClassList);
        Assert.DoesNotContain("munin-explorer-list-menu--end", cut.Find(DownloadToggle).ParentElement!.ClassList);
    }

    private static bool Open(IRenderedComponent<VariableListView> cut, string panel) =>
        !cut.Find(panel).HasAttribute("hidden");

    [Fact]
    public void Folds_WhenOneOpens_ThenTheOtherCloses()
    {
        // Both panels hang over the rows below, so two open at once paint one over the other.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        cut.Find(DownloadToggle).Click();

        OpenMenu(cut);
        Assert.False(Open(cut, DownloadPanel));

        cut.Find(DownloadToggle).Click();
        Assert.False(Open(cut, MenuPanel));
        Assert.True(Open(cut, DownloadPanel));
    }

    [Fact]
    public async Task Folds_WhenOpeningTheDownloadClosesFlereValg_ThenAnArmedDeleteIsDisarmed()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);
        await ChooseAsync(cut, "Slett listen");

        cut.Find(DownloadToggle).Click();

        Assert.Equal("false", Disclosed(cut, DeleteToggle));
    }

    [Fact]
    public void Folds_WhenCreateIsPressed_ThenBothClose()
    {
        // The create form opens under the row, where an open panel would cover it.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);

        cut.Find("button[id^='munin-explorer-create-toggle-']").Click();

        Assert.False(Open(cut, MenuPanel));
        Assert.Single(cut.FindAll("input[id^='munin-explorer-new-list-']"));
    }

    [Fact]
    public void Folds_WhenCreateIsPressedWithTheDownloadOpen_ThenItClosesToo()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        cut.Find(DownloadToggle).Click();

        cut.Find("button[id^='munin-explorer-create-toggle-']").Click();

        Assert.False(Open(cut, DownloadPanel));
    }

    [Fact]
    public async Task Folds_WhenAnotherListIsChosenWithFlereValgOpen_ThenItIsClosed()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2 });
        OpenMenu(cut);
        var second = cut.FindAll("select option")[1].GetAttribute("value");

        await cut.InvokeAsync(() => cut.Find("select").Change(second));

        cut.WaitForAssertion(() => Assert.False(Open(cut, MenuPanel)));
    }

    [Fact]
    public async Task Folds_WhenAnotherListIsChosen_ThenBothAreClosed()
    {
        // A fold left open over a list the reader did not open it for, as the forms already are not.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2 });
        cut.Find(DownloadToggle).Click();
        var second = cut.FindAll("select option")[1].GetAttribute("value");

        await cut.InvokeAsync(() => cut.Find("select").Change(second));

        cut.WaitForAssertion(() => Assert.False(Open(cut, DownloadPanel)));
    }

    [Fact]
    public async Task Menu_WhenTheDeleteIsCancelled_ThenItStaysOpenOnTheControl()
    {
        // «Avbryt» is the same control as «Slett listen», so the reader is still standing in the fold.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        OpenMenu(cut);
        await ChooseAsync(cut, "Slett listen");

        await ChooseAsync(cut, "Avbryt");

        Assert.True(Open(cut, MenuPanel));
        Assert.Equal("false", Disclosed(cut, DeleteToggle));
    }

    [Fact]
    public async Task Menu_WhenSharingIsChosenWhileARenameFinishes_ThenFocusStaysOnTheCode()
    {
        // Sharing is the reader moving on, as opening any other form is; the rename must not pull them back.
        var renameHeld = new TaskCompletionSource();
        var client = new ShareClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { DuringRename = () => renameHeld.Task };
        var cut = RenderView(client);
        RenameField(cut).Change("Hjertet mitt");
        var rename = PressAsync(cut, "Lagre navnet");

        OpenMenu(cut);
        await ChooseAsync(cut, "Del liste");
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_shareCodeField"), FocusedId()));
        await cut.InvokeAsync(renameHeld.SetResult);
        await rename;

        cut.WaitForAssertion(() => Assert.Equal("Hjertet mitt", ListHeading(cut).TextContent));
        await cut.InvokeAsync(() => { });
        Assert.Equal(Held(cut, "_shareCodeField"), FocusedId());
    }
}
