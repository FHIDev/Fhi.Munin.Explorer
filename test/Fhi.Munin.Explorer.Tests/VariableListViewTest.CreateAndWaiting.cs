using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The heading row, the new-list form and what the view says while a call is out
/// (Fhi.Metadata-fc7ie, Fhi.Metadata-aetys).
/// </summary>
public partial class VariableListViewTest
{
    private const string NewListToggle = "button[id^='munin-explorer-create-toggle-']";
    private const string CreateForm = ".munin-explorer-list-create";
    private const string Placeholder = ".munin-explorer-list-placeholder";

    private static AngleSharp.Dom.IElement? Submit(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll($"{CreateForm} button.button-square--secondary").SingleOrDefault();

    [Fact]
    public void Head_WhenAListIsShown_ThenTheNameAndTheActionsShareOneRowWithCreateLast()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var head = cut.Find(".munin-explorer-list-head");

        Assert.Equal(
            ["munin-explorer-page__header", "munin-explorer-page__actions"],
            head.Children.Select(child => child.ClassName));
        Assert.Equal(cut.Find(NewListToggle), head.QuerySelectorAll("button").Last(b => b.Closest(MenuPanel) is null));
    }

    [Fact]
    public void Form_WhenTheReaderCancels_ThenItFoldsAndTheToggleSaysSo()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        CreateField(cut).Change("Hjerte og kar");
        Press(cut, "Avbryt");

        Assert.Empty(cut.FindAll(CreateForm));
        Assert.Equal("false", cut.Find(NewListToggle).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Form_WhenEscapeIsPressedInIt_ThenItFolds()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        CreateField(cut);
        cut.Find(CreateForm).KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(CreateForm));
    }

    [Fact]
    public void Form_WhenAnotherKeyIsPressedInIt_ThenItStays()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        CreateField(cut);
        cut.Find(CreateForm).KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Single(cut.FindAll(CreateForm));
    }

    [Fact]
    public async Task Create_WhileTheCallIsOut_ThenTheButtonSaysSoAndASecondPressDoesNothing()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { DuringCreate = () => gate.Task };
        var cut = RenderView(client);

        CreateField(cut).Change("Kreft og svulster");
        await PressAsync(cut, "Opprett liste");

        await cut.WaitForAssertionAsync(() => Assert.Equal("Oppretter …", Submit(cut)!.TextContent.Trim()));
        Assert.Equal("true", Submit(cut)!.GetAttribute("aria-busy"));
        Assert.Equal("true", Submit(cut)!.GetAttribute("aria-disabled"));
        Assert.True(cut.Find("input[id^='munin-explorer-new-list-']").HasAttribute("readonly"));

        // The double press, and a cancel under a create that is still out.
        await cut.InvokeAsync(() => Submit(cut)!.Click());
        await PressAsync(cut, "Avbryt");

        Assert.Equal(1, client.CreateCalls);
        Assert.Single(cut.FindAll(CreateForm));
        Assert.DoesNotContain("Listen er opprettet.", cut.Markup);

        gate.SetResult();

        await cut.WaitForAssertionAsync(() => Assert.Contains("Listen er opprettet.", cut.Markup));
        Assert.Empty(cut.FindAll(CreateForm));
        Assert.Contains("Kreft og svulster", cut.Markup);
    }

    [Fact]
    public void Create_WhenItFails_ThenTheFormStaysOpenWithTheNameAndTheButtonIsItselfAgain()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { CreateThrows = true };
        var cut = RenderView(client);

        CreateField(cut).Change("Hjerte og kar");
        Press(cut, "Opprett liste");

        Assert.Equal("Opprett liste", Submit(cut)!.TextContent.Trim());
        Assert.Null(Submit(cut)!.GetAttribute("aria-busy"));
        Assert.Equal("Hjerte og kar", cut.Find("input[id^='munin-explorer-new-list-']").GetAttribute("value"));
        Assert.False(cut.Find("input[id^='munin-explorer-new-list-']").HasAttribute("readonly"));
        Assert.DoesNotContain("Listen er opprettet.", cut.Markup);
        Assert.NotEmpty(cut.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public async Task Rows_WhileTheListIsBeingRead_ThenItSaysSoAndNeverThatTheListIsEmpty()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { StallPageReads = true };
        var cut = RenderView(client);

        await cut.WaitForAssertionAsync(() => Assert.Single(cut.FindAll(Placeholder)));

        Assert.Contains("Henter variablene i listen …", cut.Find("p[role='status']").TextContent);
        Assert.Equal("true", cut.Find(Placeholder).GetAttribute("aria-hidden"));
        Assert.InRange(cut.FindAll($"{Placeholder} > li").Count, 1, 5);
        Assert.DoesNotContain("Denne listen er tom", cut.Markup);
        Assert.DoesNotContain("Du har ingen variabellister", cut.Markup);
        Assert.Empty(cut.FindAll(".emptystate"));

        client.StallPageReads = false;
        client.ReleaseVariables();

        await cut.WaitForAssertionAsync(() => Assert.Contains("Alder ved diagnose", cut.Markup));
        Assert.Empty(cut.FindAll(Placeholder));
        Assert.DoesNotContain("Henter variablene i listen", cut.Markup);
    }

    [Fact]
    public async Task Rows_WhenTheReadFails_ThenThePlaceholderLeavesForTheFailure()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { PageReadThrows = true };
        var cut = RenderView(client);

        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.Find("[role='alert']").TextContent.Trim()));

        Assert.Empty(cut.FindAll(Placeholder));
        Assert.DoesNotContain("Denne listen er tom", cut.Markup);
    }

    [Fact]
    public async Task Texts_WhenTheLanguageIsEnglish_ThenTheNewWordsAreEnglish()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ListClient(Item("Age at diagnosis", "V_BDR.ALDER")) { DuringCreate = () => gate.Task };
        var cut = RenderView(client, language: "en");

        Assert.Equal("Add new list", cut.Find(NewListToggle).TextContent.Trim());

        CreateField(cut).Change("Heart");
        Assert.Contains("Cancel", cut.Find(CreateForm).TextContent);
        await PressAsync(cut, "Create list");

        await cut.WaitForAssertionAsync(() => Assert.Equal("Creating …", Submit(cut)!.TextContent.Trim()));

        gate.SetResult();

        await cut.WaitForAssertionAsync(() => Assert.Contains("The list has been created.", cut.Markup));
    }
}
