using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A signed-out reader's variable list, kept in their browser when the host turns
/// <c>MuninExplorer:Features:AnonymousLists</c> on (Fhi.Metadata-emsc0.3.12). The browser's half is
/// <see cref="FakeBrowserList"/>; <see cref="BrowserListStoreTest"/> pins the module's own shapes.
/// </summary>
public class AnonymousListTest : ExplorerTestContext
{
    private const string Focus = "Blazor._internal.domWrapper.focus";

    private static readonly Texts No = Texts.For("no");
    private static readonly Guid OwnListId = Guid.NewGuid();

    private static VariableSummary Row(string name, string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        PreferredTerm = name,
        KildeName = "Als registeret",
        DatasamlingId = Guid.NewGuid(),
        DatasamlingName = "Inklusjon",
        RowKey = code,
    };

    private static readonly VariableSummary Alder = Row("Alder ved diagnose", "V_ALDER");

    private static VariableListItem Stored(VariableSummary v) => new()
    {
        VariableId = v.Id,
        VariableCode = v.Code,
        VariableName = v.PreferredTerm,
        DatasamlingId = v.DatasamlingId,
    };

    private sealed class Client(params VariableSummary[] rows) : EmptyMuninExplorerClient
    {
        public int MyListsCalls { get; private set; }
        public List<IReadOnlyCollection<VariableDatasamlingKey>> Exported { get; } = [];
        public List<(string Name, IReadOnlyCollection<VariableListItem> Items)> Shared { get; } = [];
        public List<string> Created { get; } = [];
        public List<IReadOnlyCollection<VariableDatasamlingKey>> Added { get; } = [];
        public List<VariableList> Lists { get; } = [];
        public bool AddThrows { get; init; }
        public Exception? ShareThrows { get; init; }
        public Task<string>? ShareAnswer { get; init; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary> { Items = rows, TotalCount = rows.Length, PageNumber = 1, Size = 25, TotalPages = 1 });

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            MyListsCalls++;
            return Task.FromResult<IReadOnlyList<VariableList>>([.. Lists]);
        }

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            Created.Add(name);
            var list = new VariableList { Id = Guid.NewGuid(), Name = name };
            Lists.Add(list);
            return Task.FromResult(list);
        }

        public Action? BeforeAdd { get; set; }
        public VariableDatasamlingKey? Stale { get; init; }

        public override Task<bool> AddItemsToMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
            BeforeAdd?.Invoke();

            if (Stale is { } stale && items.Contains(stale))
            {
                throw new HttpRequestException("stale datasamling", null, System.Net.HttpStatusCode.BadRequest);
            }

            if (AddThrows)
            {
                throw new HttpRequestException("nede");
            }

            Added.Add(items);
            return Task.FromResult(true);
        }

        public override Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Lists.RemoveAll(l => l.Id == id);
            return Task.FromResult(true);
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem> { Items = [], TotalCount = 0, PageNumber = 1, Size = pageSize, TotalPages = 1 });

        public override Task<ExportedList> ExportItemsAsync(
            IReadOnlyCollection<VariableDatasamlingKey> items, Uri? explorerPageUrl = null,
            ExportFormat format = ExportFormat.Xlsx, bool includeKodeverk = false, CancellationToken cancellationToken = default)
        {
            Exported.Add(items);
            return Task.FromResult(new ExportedList([1], "application/octet-stream", "Variabelliste.xlsx"));
        }

        public override Task<string> ShareListAsync(
            string name, IReadOnlyCollection<VariableListItem> items, CancellationToken cancellationToken = default)
        {
            Shared.Add((name, items));

            if (ShareThrows is { } thrown)
            {
                throw thrown;
            }

            return ShareAnswer ?? Task.FromResult("ABC123");
        }
    }

    /// <summary>The switch as the host sets it, and the browser already reached when <paramref name="browser"/> is given.</summary>
    private void Register(Client client, bool? flag, FakeBrowserList? browser = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped(_ =>
        {
            var state = new VariableListState(client);

            if (browser is not null)
            {
                state.OfferBrowserList(browser);
            }

            return state;
        });

        if (flag is { } on)
        {
            Services.AddSingleton(new AnonymousListsSwitch(on));
        }
    }

    private IRenderedComponent<VariableSearch> Search(Client client, FakeBrowserList? browser, bool? flag = true, bool signedIn = false)
    {
        Register(client, flag, browser);
        RenderFragment list = b => b.AddContent(0, "the list");
        return Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, signedIn).Add(c => c.VariableList, list));
    }

    private IRenderedComponent<VariableListView> ListView(Client client, FakeBrowserList? browser, bool signedIn = false)
    {
        Register(client, flag: true, browser);
        return Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, signedIn));
    }

    private static IElement SaveButton(IRenderedComponent<VariableSearch> cut)
    {
        var toggle = cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name");

        if (toggle.GetAttribute("aria-expanded") != "true")
        {
            toggle.Click(new MouseEventArgs { Detail = 1 });
        }

        return cut.Find(".munin-explorer-detail button[aria-pressed]");
    }

    private static string Status(IRenderedComponent<VariableSearch> cut) =>
        cut.Find(".munin-explorer-detail .munin-explorer-data-list__save-status [role=alert]").TextContent.Trim();

    private static IElement ButtonSaying<T>(IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private static List<string> RowNames<T>(IRenderedComponent<T> cut) where T : IComponent =>
        [.. cut.FindAll("table.munin-explorer-data-list tbody th[scope=row] .munin-explorer-dataitem-main__column__text").Select(c => c.TextContent.Trim())];

    // -----------------------------------------------------------------------
    // Flag off changes nothing

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void VariableSearch_WhenTheFlagIsOff_ThenASignedOutReaderHasNoSaveButtonNoTabsAndNoStorageCall(bool? flag)
    {
        // Off is the default, and a host that has not opted in must see the signed-out page it always had.
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        var cut = Search(new Client(Alder), browser: null, flag);

        cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click(new MouseEventArgs { Detail = 1 });

        Assert.Empty(cut.FindAll(".munin-explorer-detail button[aria-pressed]"));
        Assert.Empty(cut.FindAll("[role=tablist]"));
        Assert.Contains(No.SignInForVariableLists, cut.Markup);
        Assert.DoesNotContain(module.Invocations, call => call.Identifier.Contains("nonymousList", StringComparison.Ordinal));
    }

    [Fact]
    public void VariableListView_WhenTheFlagIsOff_ThenASignedOutReaderGetsNothing()
    {
        Register(new Client(), flag: false);

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, false));

        Assert.Equal("", cut.Markup.Trim());
    }

    [Fact]
    public void VariableSearch_WhenMountedWithoutAListTab_ThenASignedOutReaderGetsNoSaveButton()
    {
        // A save nothing on the page can show is a control that seems to do nothing.
        Register(new Client(Alder), flag: true, new FakeBrowserList());
        var cut = Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, false));

        cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click(new MouseEventArgs { Detail = 1 });

        Assert.Empty(cut.FindAll(".munin-explorer-detail button[aria-pressed]"));
    }

    [Fact]
    public void VariableSearch_WhenTheFlagIsOn_ThenASignedOutReaderGetsTheListTab()
    {
        // The real path: the switch is what makes the search offer the browser list, with no store handed in.
        var cut = Search(new Client(Alder), browser: null, flag: true);

        Assert.Single(cut.FindAll("[role=tablist]"));
        Assert.DoesNotContain(No.SignInForVariableLists, cut.Markup);
    }

    // -----------------------------------------------------------------------
    // Adding and removing

    [Fact]
    public void VariableSearch_WhenSignedOutAndSavePressed_ThenTheRowGoesToTheBrowserAndNothingToMunin()
    {
        var browser = new FakeBrowserList();
        var client = new Client(Alder);
        var cut = Search(client, browser);

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed")));
        var row = Assert.Single(browser.Rows);
        Assert.Equal(VariableDatasamlingKey.Of(Alder), VariableDatasamlingKey.Of(row));
        Assert.Equal("V_ALDER", row.VariableCode);
        Assert.Equal(0, client.MyListsCalls);
        Assert.Empty(client.Created);
    }

    [Fact]
    public void VariableSearch_WhenTheRowIsAlreadyInTheBrowserList_ThenThePressTakesItOut()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(Stored(Alder));
        var cut = Search(new Client(Alder), browser);
        cut.WaitForAssertion(() => Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed")));

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed")));
        Assert.Empty(browser.Rows);
    }

    [Fact]
    public void VariableSearch_WhenTheBrowserListHoldsTwoThousandRows_ThenTheRowIsRefusedWithTheReason()
    {
        // The anonymous export's own ceiling: a list it could not download is not a list worth growing.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Enumerable.Range(0, VariableListState.MaxBrowserListRows).Select(n => FakeBrowserList.Row($"v{n}")));
        var cut = Search(new Client(Alder), browser);

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal(No.BrowserListFull(2000), Status(cut)));
        Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Equal(2000, browser.Rows.Count);
    }

    [Fact]
    public void VariableSearch_WhenTheBrowserRefusesStorage_ThenTheRowIsKeptForThePageAndTheReaderIsTold()
    {
        var cut = Search(new Client(Alder), new FakeBrowserList { Refuses = true });

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal(No.BrowserListNotKept, Status(cut)));
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VariableSearch_WhenTheBrowserCannotBeAsked_ThenThePressSaysSoAndTheCircuitStays(bool throws)
    {
        // No module answers null; a fault inside it throws across the circuit. Neither may take the page down.
        var browser = throws
            ? new FakeBrowserList { Throws = new Microsoft.JSInterop.JSException("broken") }
            : new FakeBrowserList { Unreachable = true };
        var cut = Search(new Client(Alder), browser);

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal(No.BrowserListUnavailable, Status(cut)));
        Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void VariableSearch_WhenSignedInWithTheFlagOn_ThenSaveGoesToMuninAndNeverToTheBrowser()
    {
        var browser = new FakeBrowserList();
        var client = new Client(Alder);
        client.Lists.Add(new VariableList { Id = OwnListId, Name = "Mine" });
        var cut = Search(client, browser, signedIn: true);

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal(VariableDatasamlingKey.Of(Alder), Assert.Single(Assert.Single(client.Added))));
        Assert.Empty(browser.Rows);
        Assert.DoesNotContain(browser.Calls, call => call.Call is "add" or "remove");
    }

    [Fact]
    public void VariableSearch_WhenTheReaderSignsInAndOutInOneCircuit_ThenTheButtonAnswersFromTheRightList()
    {
        // The browser list is the signed-out reader's; signed in, the same row reads from Munin's list, which lacks it.
        var browser = new FakeBrowserList();
        browser.Rows.Add(Stored(Alder));
        var client = new Client(Alder);
        client.Lists.Add(new VariableList { Id = OwnListId, Name = "Mine" });
        var cut = Search(client, browser);
        cut.WaitForAssertion(() => Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed")));

        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForAssertion(() => Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed")));

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.WaitForAssertion(() => Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed")));
    }

    // -----------------------------------------------------------------------
    // Showing it

    [Fact]
    public void VariableListView_WhenABrowserListIsStored_ThenItIsReadBackOnMountWithTheSentences()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Alder ved diagnose"), FakeBrowserList.Row("Kjønn")]);

        var cut = ListView(new Client(), browser);

        cut.WaitForAssertion(() => Assert.Equal(["Alder ved diagnose", "Kjønn"], RowNames(cut)));
        Assert.Contains(No.FirstListName, cut.Markup);
        Assert.Contains(No.BrowserListKeptHere, cut.Markup);
        Assert.Contains(No.BrowserListApplication, cut.Markup);
        Assert.Contains(No.BrowserListSignInFor, cut.Markup);
        Assert.DoesNotContain(No.BrowserListNotKept, cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenTheListIsLong_ThenNoCallCarriesMoreThanASmallChunk()
    {
        // A JS answer on Blazor Server is capped at 32 KB: a whole list of rows in one answer drops the circuit.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange(Enumerable.Range(0, 450).Select(n => FakeBrowserList.Row($"Variabel {n:000}")));

        var cut = ListView(new Client(), browser);

        cut.WaitForAssertion(() => Assert.Equal(25, RowNames(cut).Count));
        Assert.Equal("Variabel 000", RowNames(cut)[0]);
        Assert.All(browser.Calls.Where(c => c.Call == "keys"), c => Assert.True(c.Count <= 100));
        Assert.All(browser.Calls.Where(c => c.Call == "page"), c => Assert.True(c.Count <= VariableListState.RowsPerCall));
        Assert.Contains(No.ListVariableCount(450), cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenARowArrivesAsItsCodeOnly_ThenItIsNamedByItsCode()
    {
        // The module hands a row too long for one answer back as its ids and code; it is not a variable that went away.
        var browser = new FakeBrowserList();
        browser.Rows.Add(new VariableListItem { VariableId = Guid.NewGuid(), VariableCode = "V_HUGE" });

        var cut = ListView(new Client(), browser);

        cut.WaitForAssertion(() => Assert.Equal(["V_HUGE"], RowNames(cut)));
        Assert.DoesNotContain(No.VariableNoLongerAvailable, cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenTheBrowserCannotStore_ThenTheViewSaysTheListIsOnlyForThisPage()
    {
        var browser = new FakeBrowserList { Refuses = true };
        browser.Rows.Add(FakeBrowserList.Row("Alder"));

        var cut = ListView(new Client(), browser);

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListNotKept, cut.Markup));
        Assert.DoesNotContain(No.BrowserListKeptHere, cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenTheHostServesNoModule_ThenTheViewSaysThereIsNoListToKeep()
    {
        var cut = ListView(new Client(), new FakeBrowserList { Unreachable = true });

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListUnavailable, cut.Markup));
    }

    [Fact]
    public void VariableListView_WhenDownloadIsPressed_ThenTheBrowserListsRowsGoToTheAnonymousExport()
    {
        var datasamling = Guid.NewGuid();
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Alder", datasamling), FakeBrowserList.Row("Kjønn")]);
        var client = new Client();
        var cut = ListView(client, browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));

        cut.Find("button[id^=munin-explorer-list-download-toggle]").Click();
        cut.FindAll("[id^=munin-explorer-list-download-] button.munin-explorer-list-menu__item")[0].Click();

        cut.WaitForAssertion(() => Assert.Equal(browser.Rows.Select(VariableDatasamlingKey.Of), Assert.Single(client.Exported)));
    }

    [Fact]
    public void VariableListView_WhenShareIsPressed_ThenTheRowsGoToTheAnonymousShareAndTheCodeIsShown()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client();
        var cut = ListView(client, browser);
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        ButtonSaying(cut, No.ShareList).Click();

        cut.WaitForAssertion(() => Assert.Equal("ABC123", cut.Find("input[id^=munin-explorer-share-code]").GetAttribute("value")));
        var (name, items) = Assert.Single(client.Shared);
        Assert.Equal(No.FirstListName, name);
        Assert.Single(items);
    }

    /// <summary>One row whose name brings the share's snapshot to exactly <paramref name="length"/> characters.</summary>
    private static VariableListItem RowOfSnapshotLength(int length)
    {
        var row = FakeBrowserList.Row("");
        var bare = MuninExplorerClient.ShareSnapshotLength(No.FirstListName, [row]);
        return row with { VariableName = new string('x', length - bare) };
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void VariableListView_WhenTheSnapshotIsAtOrPastTheShareCap_ThenItIsSentOrRefused(int past, bool sent)
    {
        // Munin stores at most 524,288 characters of snapshot; past the margin under it the share is refused here.
        var browser = new FakeBrowserList();
        browser.Rows.Add(RowOfSnapshotLength(MuninExplorerClient.MaxShareSnapshotChars + past));
        var client = new Client();
        var cut = ListView(client, browser);
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        ButtonSaying(cut, No.ShareList).Click();

        if (sent)
        {
            cut.WaitForAssertion(() => Assert.Single(client.Shared));
            Assert.DoesNotContain(No.BrowserListTooLongToShare, cut.Markup);
        }
        else
        {
            cut.WaitForAssertion(() => Assert.Contains(No.BrowserListTooLongToShare, cut.Markup));
            Assert.Empty(client.Shared);
        }
    }

    [Fact]
    public void VariableListView_WhenARowCameBackAsItsCodeOnly_ThenTheShareIsRefusedRatherThanSentShort()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Alder"), FakeBrowserList.Row("Kjønn")]);
        browser.Truncates.Add(VariableDatasamlingKey.Of(browser.Rows[1]));
        var client = new Client();
        var cut = ListView(client, browser);
        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));

        ButtonSaying(cut, No.ShareList).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListReadError, cut.Markup));
        Assert.Empty(client.Shared);
    }

    [Fact]
    public async Task VariableListView_WhenTheListChangesWhileClearIsAsked_ThenTheQuestionIsWithdrawn()
    {
        // The question was about the rows on screen when it was asked; another tab changed them.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var cut = ListView(new Client(), browser);
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));
        ButtonSaying(cut, No.ClearBrowserList).Click();
        Assert.Contains(No.ConfirmClearBrowserList, cut.Markup);

        browser.Rows.Add(FakeBrowserList.Row("Kjønn"));
        await cut.InvokeAsync(browser.ChangedElsewhereAsync);

        cut.WaitForAssertion(() => Assert.DoesNotContain(No.ConfirmClearBrowserList, cut.Markup));
        Assert.Equal("false", ButtonSaying(cut, No.ClearBrowserList).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void VariableListView_WhenTheCarriedRowsWereOnlyInThisPagesMemory_ThenItSaysSavedAndNothingElse()
    {
        var browser = new FakeBrowserList { Refuses = true };
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var cut = ListView(new Client(), browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListSavedAs(No.FirstListName), cut.Markup));
        Assert.DoesNotContain(No.BrowserListSavedButStillHere(No.FirstListName), cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenMuninRefusesTheShareAsTooLarge_ThenTheReaderGetsTheSameSentence()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var cut = ListView(new Client { ShareThrows = new SharedSnapshotTooLargeException() }, browser);
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        ButtonSaying(cut, No.ShareList).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListTooLongToShare, cut.Markup));
    }

    [Fact]
    public async Task VariableListView_WhenTheListChangesWhileTheShareIsOut_ThenTheStaleCodeIsNotShown()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var share = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { ShareAnswer = share.Task };
        var cut = ListView(client, browser);
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        ButtonSaying(cut, No.ShareList).Click();
        cut.WaitForAssertion(() => Assert.Single(client.Shared));
        browser.Rows.Add(FakeBrowserList.Row("Kjønn"));
        await cut.InvokeAsync(browser.ChangedElsewhereAsync);
        share.SetResult("ABC123");

        cut.WaitForAssertion(() => Assert.Equal(2, RowNames(cut).Count));
        Assert.Empty(cut.FindAll("input[id^=munin-explorer-share-code]"));

        // Asked again, the share is made of the list as it is now, not handed the stale code.
        ButtonSaying(cut, No.ShareList).Click();

        cut.WaitForAssertion(() => Assert.Equal(2, client.Shared.Count));
        Assert.Equal(2, client.Shared[^1].Items.Count);
    }

    [Fact]
    public void VariableListView_WhenTheKeyHoldsSomethingThisVersionCannotRead_ThenItSaysSo()
    {
        var cut = ListView(new Client(), new FakeBrowserList { Foreign = true });

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListForeign, cut.Markup));
        Assert.DoesNotContain(No.BrowserListNotKept, cut.Markup);
    }

    [Fact]
    public async Task VariableListView_WhenThePageCannotBeRead_ThenItSaysSoAndReadsAgainOnTheNextChange()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        browser.Failing.Add("page");
        var cut = ListView(new Client(), browser);

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListReadError, cut.Markup));
        var reads = browser.Calls.Count(c => c.Call == "page");

        browser.Failing.Remove("page");
        await cut.InvokeAsync(browser.ChangedElsewhereAsync);

        cut.WaitForAssertion(() => Assert.Equal(["Alder"], RowNames(cut)));
        Assert.True(browser.Calls.Count(c => c.Call == "page") > reads);
        Assert.DoesNotContain(No.BrowserListReadError, cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenClearIsPressedOnce_ThenItOnlyAsksAndTheSecondStepEmptiesTheListAndReturnsFocus()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var cut = ListView(new Client(), browser);
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        ButtonSaying(cut, No.ClearBrowserList).Click();

        Assert.Contains(No.ConfirmClearBrowserList, cut.Markup);
        Assert.Single(browser.Rows);
        Assert.Empty(JSInterop.Invocations[Focus]);

        ButtonSaying(cut, No.ConfirmEmptyYes).Click();

        cut.WaitForAssertion(() => Assert.Empty(RowNames(cut)));
        Assert.Empty(browser.Rows);
        Assert.Contains(No.EmptyList, cut.Markup);

        // The yes button left with the question; the control that asked it stays, refused, and takes focus. Only a
        // reference to it can be focused here: the yes button is gone and nothing else asks for focus in this view.
        Assert.Equal("true", ButtonSaying(cut, No.ClearBrowserList).GetAttribute("aria-disabled"));
        cut.WaitForAssertion(() => Assert.Single(JSInterop.Invocations[Focus]));
    }

    // -----------------------------------------------------------------------
    // Signed in with a browser list

    [Fact]
    public void VariableListView_WhenSignedInWithABrowserList_ThenOnlyTheOfferIsDrawnNotTheList()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client();

        var cut = ListView(client, browser, signedIn: true);

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        Assert.DoesNotContain(No.BrowserListKeptHere, cut.Markup);
        Assert.Empty(RowNames(cut));

        // Never on its own: nothing is made until the reader presses.
        Assert.Empty(client.Created);
    }

    [Fact]
    public void VariableListView_WhenTheCarryOverIsPressed_ThenItSavesANewListEmptiesTheBrowserAndSaysSo()
    {
        var datasamling = Guid.NewGuid();
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder", datasamling));
        var client = new Client();
        client.Lists.Add(new VariableList { Id = OwnListId, Name = "Min variabelliste" });
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        var keys = browser.Rows.Select(VariableDatasamlingKey.Of).ToList();

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListSavedAs("Min variabelliste (2)"), cut.Markup));
        Assert.DoesNotContain(No.BrowserListWaiting(1), cut.Markup);
        Assert.Equal(["Min variabelliste (2)"], client.Created);
        Assert.Equal(keys, Assert.Single(client.Added));
        Assert.Empty(browser.Rows);

        // The button left with its offer; focus goes to the picker that now names the new list.
        cut.WaitForAssertion(() => JSInterop.Invocations[Focus][^1].Arguments[0].ShouldBeElementReferenceTo(cut.Find("select")));
    }

    [Fact]
    public void VariableListView_WhenTheCarryOverMakesTheReadersOnlyList_ThenFocusGoesToCreatingAnother()
    {
        // With one list there is no picker to name it, so focus goes where a created list's does: «Legg til ny liste».
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client();
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListSavedAs(No.FirstListName), cut.Markup));

        // The toggle lives in a fragment whose reference bUnit does not print, so the call is counted, not compared;
        // nothing else in this view asks for focus after the press.
        Assert.Empty(cut.FindAll("select"));
        cut.WaitForAssertion(() => Assert.Single(JSInterop.Invocations[Focus]));
    }

    [Fact]
    public void VariableListView_WhenARowArrivesFromAnotherTabDuringTheCarryOver_ThenItStaysInTheBrowser()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client();
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        var late = FakeBrowserList.Row("Kjønn");
        client.BeforeAdd = () => browser.Rows.Add(late);

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Equal([VariableDatasamlingKey.Of(late)], browser.Rows.Select(VariableDatasamlingKey.Of)));
    }

    [Fact]
    public void VariableListView_WhenTheSavedRowsCannotLeaveTheBrowser_ThenItSaysSoAndOffersNoSecondCarryOver()
    {
        // Offered again, the same rows would be saved as a second list.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        browser.Failing.Add("removeKeys");
        var client = new Client();
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListSavedButStillHere(No.FirstListName), cut.Markup));
        Assert.DoesNotContain(No.BrowserListSavedAs(No.FirstListName), cut.Markup);
        Assert.DoesNotContain(No.CarryBrowserListOver, cut.Markup);
        Assert.Single(client.Created);
    }

    [Fact]
    public void VariableListView_WhenOneRowIsStale_ThenTheRestIsSavedAndTheReaderIsToldHowManyStayed()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Alder"), FakeBrowserList.Row("Gammel")]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[1]) };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListNotCarried(1), cut.Markup));
        Assert.Contains(No.BrowserListSavedAs(No.FirstListName), cut.Markup);
        Assert.Single(browser.Rows);
    }

    [Fact]
    public void VariableListView_WhenTheCarryOverFails_ThenTheBrowserListStaysAndTheReaderIsTold()
    {
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client { AddThrows = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.CarryBrowserListOverError, cut.Markup));
        Assert.Contains(No.BrowserListWaiting(1), cut.Markup);
        Assert.Single(browser.Rows);

        // The half-made list is taken away again rather than left empty among the reader's own.
        Assert.Empty(client.Lists);
    }
}
