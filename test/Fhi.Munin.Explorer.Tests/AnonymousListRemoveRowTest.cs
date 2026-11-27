using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A signed-out reader's «Fjern» on one row of the list kept in the browser (Fhi.Metadata-emsc0.3.13): the row goes,
/// the others stay, and focus stays in the list.
/// </summary>
public class AnonymousListRemoveRowTest : ExplorerTestContext
{
    private const string FocusIfLost = "focusIfLost";

    private static readonly Texts No = Texts.For("no");

    private sealed class Client : EmptyMuninExplorerClient;

    private BunitJSModuleInterop? _module;

    private IRenderedComponent<VariableListView> ListView(FakeBrowserList browser, int pageSize = 25)
    {
        var client = new Client();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped(_ =>
        {
            var state = new VariableListState(client);
            state.OfferBrowserList(browser);
            return state;
        });
        Services.AddSingleton(new CodePickerSwitch(true));
        _module = PlanExplorerModule();

        return Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, false).Add(c => c.PageSize, pageSize));
    }

    private static FakeBrowserList Holding(params string[] names)
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(names.Select(n => FakeBrowserList.Row(n)));
        return browser;
    }

    private static List<string> RowNames(IRenderedComponent<VariableListView> cut) =>
        [.. cut.FindAll("table.munin-explorer-data-list tbody th[scope=row] .munin-explorer-dataitem-main__column__text").Select(c => c.TextContent.Trim())];

    private static IElement RemoveButtonOf(IRenderedComponent<VariableListView> cut, string name) =>
        cut.FindAll("table.munin-explorer-data-list tbody tr")
            .Single(r => r.QuerySelector("th[scope=row] .munin-explorer-dataitem-main__column__text")!.TextContent.Trim() == name)
            .QuerySelector("button")!;

    private static string Status(IRenderedComponent<VariableListView> cut) =>
        cut.Find("[role=status]").TextContent.Trim();

    // The id the browser is asked to move focus to, once the reader's place has left with the pressed «Fjern».
    private string FocusedId(IRenderedComponent<VariableListView> cut) =>
        (string)FocusAsks[^1].Arguments[0]!;

    private IReadOnlyList<JSRuntimeInvocation> FocusAsks => _module!.Invocations[FocusIfLost];



    [Fact]
    public void VariableListView_WhenTheBrowserListShows_ThenEveryRowHasARemoveButtonNamedByItsVariable()
    {
        var cut = ListView(Holding("Alder", "Kjønn"));
        cut.WaitForAssertion(() => Assert.Equal(["Alder", "Kjønn"], RowNames(cut)));

        Assert.Contains(cut.FindAll("thead th"), h => h.TextContent.Trim() == No.ColumnActions);
        Assert.Equal($"{No.RemoveFromThisList} Alder", AccessibleName.Of(RemoveButtonOf(cut, "Alder")));
        Assert.Equal($"{No.RemoveFromThisList} Kjønn", AccessibleName.Of(RemoveButtonOf(cut, "Kjønn")));
    }

    [Fact]
    public void VariableListView_WhenARowIsRemoved_ThenOnlyThatRowLeavesTheBrowserAndFocusGoesToTheRowNowInItsPlace()
    {
        var browser = Holding("Alder", "Kjønn", "Vekt");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        var vekt = RemoveButtonOf(cut, "Vekt").Id;

        RemoveButtonOf(cut, "Kjønn").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Alder", "Vekt"], RowNames(cut)));
        Assert.Equal(["Alder", "Vekt"], browser.Rows.Select(r => r.VariableName));
        Assert.Equal(No.RemovedFromList("Kjønn"), Status(cut));
        cut.WaitForAssertion(() => Assert.Equal(vekt, FocusedId(cut)));
    }

    [Fact]
    public void VariableListView_WhenTheLastRowOfThePageIsRemoved_ThenFocusGoesToTheRowBeforeIt()
    {
        var cut = ListView(Holding("Alder", "Kjønn"));
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        var alder = RemoveButtonOf(cut, "Alder").Id;

        RemoveButtonOf(cut, "Kjønn").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Alder"], RowNames(cut)));
        cut.WaitForAssertion(() => Assert.Equal(alder, FocusedId(cut)));
    }

    [Fact]
    public void VariableListView_WhenTheOnlyRowOfTheLastPageIsRemoved_ThenThePageBeforeShowsAndItsLastRowTakesFocus()
    {
        var cut = ListView(Holding("Alder", "Kjønn", "Vekt"), pageSize: 2);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        cut.FindAll("button").Single(b => b.GetAttribute("aria-label") == No.NextLabel).Click();
        cut.WaitForAssertion(() => Assert.Equal(["Vekt"], RowNames(cut)));

        RemoveButtonOf(cut, "Vekt").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Alder", "Kjønn"], RowNames(cut)));
        cut.WaitForAssertion(() => Assert.Equal(RemoveButtonOf(cut, "Kjønn").Id, FocusedId(cut)));
    }

    [Fact]
    public void VariableListView_WhenTheListsLastRowIsRemoved_ThenTheEmptyListShowsAndTheHeadingTakesFocus()
    {
        var browser = Holding("Alder");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        RemoveButtonOf(cut, "Alder").Click();

        cut.WaitForAssertion(() => Assert.Contains(No.EmptyList, cut.Markup));
        Assert.Empty(browser.Rows);

        // The heading can take focus only from code.
        Assert.Equal("-1", cut.Find("h2, h1, h3").GetAttribute("tabindex"));
        cut.WaitForAssertion(() => Assert.Equal(cut.Find("[id^=munin-explorer-list-heading-]").Id, FocusedId(cut)));
    }

    [Fact]
    public async Task VariableListView_WhenTheBrowserCannotRemoveTheRow_ThenItStaysAndTheReaderIsToldAndFocusStays()
    {
        var browser = Holding("Alder", "Kjønn");
        browser.Failing.Add("remove");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));

        RemoveButtonOf(cut, "Alder").Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRowNotRemoved, cut.Find("[role=alert]").TextContent));
        await SettleAsync(cut);
        Assert.Equal(2, browser.Rows.Count);
        Assert.Equal("", Status(cut));
        Assert.Empty(FocusAsks);
    }

    [Fact]
    public async Task VariableListView_WhenRemoveIsPressedTwiceWhileTheFirstIsOut_ThenTheBrowserIsAskedOnce()
    {
        var browser = Holding("Alder", "Kjønn");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        browser.Hold = true;

        var button = RemoveButtonOf(cut, "Alder");
        var first = cut.InvokeAsync(() => button.Click());
        await cut.InvokeAsync(() => RemoveButtonOf(cut, "Alder").Click());

        Assert.Single(browser.Calls, c => c.Call == "remove");

        browser.Hold = false;
        while (browser.Pending.Count > 0)
        {
            await cut.InvokeAsync(browser.ReleaseNewest);
        }

        await first;
        cut.WaitForAssertion(() => Assert.Equal(["Kjønn"], RowNames(cut)));
        Assert.Single(browser.Calls, c => c.Call == "remove");
    }

    // The rows show one render before the focus step after it runs: a render and an empty turn let that step finish.
    private static async Task SettleAsync(IRenderedComponent<VariableListView> cut)
    {
        await cut.InvokeAsync(() => cut.Render());
        await cut.InvokeAsync(() => { });
    }

    private static async Task ReleaseAllAsync(IRenderedComponent<VariableListView> cut, FakeBrowserList browser)
    {
        browser.Hold = false;
        while (browser.Pending.Count > 0)
        {
            await cut.InvokeAsync(browser.ReleaseNewest);
        }
    }

    // Holds the removal's answer, then lets it through and holds the page read it sets off.
    private static async Task HoldThePageReadAfterTheRemovalAsync(IRenderedComponent<VariableListView> cut, FakeBrowserList browser, string name)
    {
        browser.Hold = true;
        await cut.InvokeAsync(() => RemoveButtonOf(cut, name).Click());
        cut.WaitForAssertion(() => Assert.Contains(browser.Pending, p => p.Call == "remove"));
        await cut.InvokeAsync(browser.ReleaseNewest);
        cut.WaitForAssertion(() => Assert.Contains(browser.Pending, p => p.Call == "page"));
    }

    [Fact]
    public async Task VariableListView_WhenThePageIsStillBeingReadBack_ThenFocusWaitsAndThenLandsOnTheRowNowInPlace()
    {
        var browser = Holding("Alder", "Kjønn", "Vekt");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        var vekt = RemoveButtonOf(cut, "Vekt").Id;

        await HoldThePageReadAfterTheRemovalAsync(cut, browser, "Kjønn");

        // A render while the read is out still draws the old page, whose «Fjern» for Kjønn is about to leave.
        await cut.InvokeAsync(() => cut.Render());
        Assert.Empty(FocusAsks);

        await ReleaseAllAsync(cut, browser);

        cut.WaitForAssertion(() => Assert.Equal(["Alder", "Vekt"], RowNames(cut)));
        cut.WaitForAssertion(() => Assert.Equal(vekt, FocusedId(cut)));
    }

    [Fact]
    public async Task VariableListView_WhenTheReaderPagesOnWhileTheRemovalIsOut_ThenFocusStaysWhereTheyAre()
    {
        var browser = Holding("Alder", "Kjønn", "Vekt", "Høyde", "Røyk");
        var cut = ListView(browser, pageSize: 2);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        browser.Hold = true;

        await cut.InvokeAsync(() => RemoveButtonOf(cut, "Alder").Click());
        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.GetAttribute("aria-label") == No.NextLabel).Click());
        await ReleaseAllAsync(cut, browser);

        cut.WaitForAssertion(() => Assert.Equal(["Høyde", "Røyk"], RowNames(cut)));
        await SettleAsync(cut);
        Assert.Equal(4, browser.Rows.Count);
        Assert.Empty(FocusAsks);
    }

    [Fact]
    public async Task VariableListView_WhenTheReaderSignsInAndOutWhileTheRemovalIsOut_ThenTheNextReaderIsToldNothingAndFocusStays()
    {
        var browser = Holding("Alder", "Kjønn");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        browser.Hold = true;

        await cut.InvokeAsync(() => RemoveButtonOf(cut, "Alder").Click());
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        await ReleaseAllAsync(cut, browser);

        cut.WaitForAssertion(() => Assert.Equal(["Kjønn"], RowNames(cut)));
        await SettleAsync(cut);
        Assert.Equal("", Status(cut));
        Assert.Empty(FocusAsks);
    }

    [Fact]
    public async Task VariableListView_WhenTheReaderSignsInWhileThePageIsReadBack_ThenNothingIsFocusedNowOrAfterSigningOut()
    {
        var browser = Holding("Alder", "Kjønn");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));

        await HoldThePageReadAfterTheRemovalAsync(cut, browser, "Alder");
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        await ReleaseAllAsync(cut, browser);
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));

        cut.WaitForAssertion(() => Assert.Equal(["Kjønn"], RowNames(cut)));
        await SettleAsync(cut);
        Assert.Empty(FocusAsks);
    }

    [Fact]
    public void VariableListView_WhenARemovalSucceedsAfterOneFailed_ThenTheFailureIsNoLongerSaid()
    {
        var browser = Holding("Alder", "Kjønn");
        browser.Failing.Add("remove");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        RemoveButtonOf(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRowNotRemoved, cut.Find("[role=alert]").TextContent));

        browser.Failing.Remove("remove");
        RemoveButtonOf(cut, "Kjønn").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Alder"], RowNames(cut)));
        Assert.DoesNotContain(No.BrowserListRowNotRemoved, cut.Find("[role=alert]").TextContent);
        Assert.Equal(No.RemovedFromList("Kjønn"), Status(cut));
    }

    [Fact]
    public void VariableListView_WhenARemovalFailsAfterOneSucceeded_ThenTheEarlierRemovalIsNoLongerSaid()
    {
        var browser = Holding("Alder", "Kjønn");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        RemoveButtonOf(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Equal(No.RemovedFromList("Alder"), Status(cut)));

        browser.Failing.Add("remove");
        RemoveButtonOf(cut, "Kjønn").Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRowNotRemoved, cut.Find("[role=alert]").TextContent));
        Assert.Equal("", Status(cut));
    }

    [Fact]
    public void VariableListView_WhenTheListIsEmptiedAfterARemoval_ThenTheRemovalIsNoLongerSaid()
    {
        var browser = Holding("Alder", "Kjønn");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        RemoveButtonOf(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Equal(No.RemovedFromList("Alder"), Status(cut)));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == No.ClearBrowserList).Click();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == No.ConfirmEmptyYes).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.EmptyList, cut.Markup));
        Assert.Equal("", Status(cut));
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("share")]
    public async Task VariableListView_WhenTheReaderOpensAQuestionOrTheShareWhileTheRemovalIsOut_ThenFocusStaysThere(string control)
    {
        // Neither goes through the folds' Toggle, so each has to count as the reader moving on by itself.
        var browser = Holding("Alder", "Kjønn", "Vekt");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        browser.Hold = true;

        await cut.InvokeAsync(() => RemoveButtonOf(cut, "Kjønn").Click());
        var opened = control == "clear" ? No.ClearBrowserList : No.ShareList;
        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == opened).Click());
        await ReleaseAllAsync(cut, browser);

        cut.WaitForAssertion(() => Assert.Equal(["Alder", "Vekt"], RowNames(cut)));
        await SettleAsync(cut);
        Assert.Empty(FocusAsks);
    }

    [Fact]
    public async Task VariableListView_WhenTheRemovalHasMovedFocus_ThenALaterRenderDoesNotMoveItAgain()
    {
        var browser = Holding("Alder", "Kjønn", "Vekt");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        RemoveButtonOf(cut, "Kjønn").Click();
        cut.WaitForAssertion(() => Assert.Single(FocusAsks));

        await SettleAsync(cut);
        await SettleAsync(cut);
        Assert.Single(FocusAsks);
    }

    [Fact]
    public async Task VariableListView_WhenARemovedRowIsSavedAgainAndRemovedAgain_ThenTheSecondPressRemovesIt()
    {
        // The row is held only until its page is read back; a row saved again from the search is a new press.
        var browser = Holding("Alder", "Kjønn");
        var alder = browser.Rows[0];
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));

        RemoveButtonOf(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Equal(["Kjønn"], RowNames(cut)));
        await cut.InvokeAsync(() => Services.GetRequiredService<VariableListState>().ToggleInBrowserListAsync(alder, drawnAsSaved: false));
        cut.WaitForAssertion(() => Assert.Equal(["Kjønn", "Alder"], RowNames(cut)));

        RemoveButtonOf(cut, "Alder").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Kjønn"], RowNames(cut)));
        Assert.Equal(2, browser.Calls.Count(c => c.Call == "remove"));
    }

    [Fact]
    public void VariableListView_WhenARemovalFailedAndIsPressedAgain_ThenTheRetryRemovesTheRow()
    {
        var browser = Holding("Alder", "Kjønn");
        browser.Failing.Add("remove");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        RemoveButtonOf(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRowNotRemoved, cut.Find("[role=alert]").TextContent));

        browser.Failing.Remove("remove");
        RemoveButtonOf(cut, "Alder").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Kjønn"], RowNames(cut)));
        Assert.Equal(No.RemovedFromList("Alder"), Status(cut));
    }

    [Fact]
    public async Task VariableListView_WhenTheListChangesInAnotherTabAfterAFailedRemoval_ThenTheFailureIsNoLongerSaid()
    {
        var browser = Holding("Alder", "Kjønn");
        browser.Failing.Add("remove");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        RemoveButtonOf(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRowNotRemoved, cut.Find("[role=alert]").TextContent));

        browser.Rows.Add(FakeBrowserList.Row("Vekt"));
        await cut.InvokeAsync(browser.ChangedElsewhereAsync);

        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        Assert.DoesNotContain(No.BrowserListRowNotRemoved, cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void VariableListView_WhenARowOfAFullPageIsRemoved_ThenFocusGoesToTheRowPulledInFromTheNextPage()
    {
        var cut = ListView(Holding("Alder", "Kjønn", "Vekt"), pageSize: 2);
        cut.WaitForAssertion(() => Assert.Equal(["Alder", "Kjønn"], RowNames(cut)));

        RemoveButtonOf(cut, "Kjønn").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Alder", "Vekt"], RowNames(cut)));
        cut.WaitForAssertion(() => Assert.Equal(RemoveButtonOf(cut, "Vekt").Id, FocusedId(cut)));
    }

    [Fact]
    public async Task VariableListView_WhenTheReaderAnswersYesToEmptyingWhileTheRemovalIsOut_ThenTheRemovalAsksForNoFocus()
    {
        // The question was open before the press, so only the yes can tell the removal the reader moved on.
        var browser = Holding("Alder", "Kjønn", "Vekt");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == No.ClearBrowserList).Click();
        browser.Hold = true;

        await cut.InvokeAsync(() => RemoveButtonOf(cut, "Kjønn").Click());
        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == No.ConfirmEmptyYes).Click());
        await ReleaseAllAsync(cut, browser);

        cut.WaitForAssertion(() => Assert.Contains(No.EmptyList, cut.Markup));
        await SettleAsync(cut);
        Assert.Empty(FocusAsks);
    }
}
