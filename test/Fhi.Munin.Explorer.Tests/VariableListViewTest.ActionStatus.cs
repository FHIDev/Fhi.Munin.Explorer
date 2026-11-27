using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A rename and a delete end with a sentence in a status region that was in the DOM before it (WCAG 4.1.3,
/// Fhi.Metadata-mhqx0.23).
/// </summary>
public partial class VariableListViewTest
{
    // The view's own status region, not the screen-reader-only one «Fjern» speaks from.
    private static AngleSharp.Dom.IElement ActionStatus(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("div[role=status]").Single(e => !e.ClassList.Contains("screenreader-only"));

    [Fact]
    public async Task Rename_WhenItSucceeds_ThenTheNewNameIsSaidFromARegionThatWasAlreadyThere()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));
        var cut = RenderView(client);
        Assert.Equal("", ActionStatus(cut).TextContent.Trim());

        RenameField(cut).Change("Hjertet mitt");
        await PressAsync(cut, "Lagre navnet");

        Assert.Equal("Listen heter nå «Hjertet mitt».", ActionStatus(cut).TextContent.Trim());
    }

    [Fact]
    public async Task Rename_WhenTheLanguageIsEn_ThenTheSentenceIsEnglish()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));
        var cut = RenderView(client, language: "en");

        RenameField(cut).Change("My heart");
        await PressAsync(cut, "Save the name");

        Assert.Equal("The list is now called “My heart”.", ActionStatus(cut).TextContent.Trim());
    }

    [Fact]
    public async Task Rename_WhenItIsThrottled_ThenNoSuccessIsSaid()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            RenameThrows = new MuninExplorerRateLimitedException()
        };
        var cut = RenderView(client);

        RenameField(cut).Change("Hjertet mitt");
        await PressAsync(cut, "Lagre navnet");

        Assert.Equal("", ActionStatus(cut).TextContent.Trim());
    }

    [Fact]
    public async Task Delete_WhenItSucceeds_ThenTheDeletedListIsNamedInTheStatus()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 3 };
        var cut = RenderView(client);
        Assert.Equal("", ActionStatus(cut).TextContent.Trim());

        await PressAsync(cut, "Slett listen");
        await PressAsync(cut, "Ja, slett listen");

        Assert.Equal(1, client.DeleteCalls);
        cut.WaitForAssertion(() =>
            Assert.Equal("Listen «Mine hjertevariabler» er slettet.", ActionStatus(cut).TextContent.Trim()));
    }

    [Fact]
    public async Task Delete_WhenItFails_ThenNoSuccessIsSaid()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            ListCount = 2,
            DeleteThrows = new InvalidOperationException("the API is unreachable")
        };
        var cut = RenderView(client);

        await PressAsync(cut, "Slett listen");
        await PressAsync(cut, "Ja, slett listen");

        Assert.Equal("", ActionStatus(cut).TextContent.Trim());
    }

    [Fact]
    public async Task Delete_WhenItFailsAfterASuccessfulRename_ThenTheRenameSentenceIsGone()
    {
        // The sentence answers for the last action only: left standing, it would read as the delete's own outcome.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            ListCount = 2,
            DeleteThrows = new InvalidOperationException("the API is unreachable")
        };
        var cut = RenderView(client);

        RenameField(cut).Change("Hjertet mitt");
        await PressAsync(cut, "Lagre navnet");
        Assert.Equal("Listen heter nå «Hjertet mitt».", ActionStatus(cut).TextContent.Trim());

        await PressAsync(cut, "Slett listen");
        await PressAsync(cut, "Ja, slett listen");

        Assert.Equal("", ActionStatus(cut).TextContent.Trim());
    }
}
