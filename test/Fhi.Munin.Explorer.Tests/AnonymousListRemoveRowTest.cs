using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A signed-out reader's «Fjern» on one row of the list kept in the browser (Fhi.Metadata-emsc0.3.13): the row goes,
/// the others stay, and focus stays in the list.
/// </summary>
public class AnonymousListRemoveRowTest : ExplorerTestContext
{
    private const string Focus = "Blazor._internal.domWrapper.focus";

    private static readonly Texts No = Texts.For("no");

    private sealed class Client : EmptyMuninExplorerClient;

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

    private string FocusedId(IRenderedComponent<VariableListView> cut) =>
        ((ElementReference)JSInterop.Invocations[Focus][^1].Arguments[0]!).Id;

    private static ElementReference Field(IRenderedComponent<VariableListView> cut, string name) =>
        (ElementReference)typeof(VariableListView)
            .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(cut.Instance)!;

    // bUnit writes an element's reference only into the render that created it, so it is read before the press.
    private static string ReferenceOf(IElement element) => element.GetAttribute("blazor:elementReference")!;

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
        var vekt = ReferenceOf(RemoveButtonOf(cut, "Vekt"));

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
        var alder = ReferenceOf(RemoveButtonOf(cut, "Alder"));

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
        cut.WaitForAssertion(() => Assert.Equal(ReferenceOf(RemoveButtonOf(cut, "Kjønn")), FocusedId(cut)));
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

        // The heading can take focus only from code; nothing else in this view asks for it after a removal.
        Assert.Equal("-1", cut.Find("h2, h1, h3").GetAttribute("tabindex"));
        cut.WaitForAssertion(() => Assert.Equal(Field(cut, "_browserHeading").Id, FocusedId(cut)));
    }

    [Fact]
    public void VariableListView_WhenTheBrowserCannotRemoveTheRow_ThenItStaysAndTheReaderIsToldAndFocusStays()
    {
        var browser = Holding("Alder", "Kjønn");
        browser.Failing.Add("remove");
        var cut = ListView(browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));

        RemoveButtonOf(cut, "Alder").Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRowNotRemoved, cut.Find("[role=alert]").TextContent));
        Assert.Equal(2, browser.Rows.Count);
        Assert.Equal("", Status(cut));
        Assert.Empty(JSInterop.Invocations[Focus]);
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
}
