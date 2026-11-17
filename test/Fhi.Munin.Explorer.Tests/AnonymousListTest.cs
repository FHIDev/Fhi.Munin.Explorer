using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        /// <summary>The read that waits for <see cref="ReadHeld"/>: «lists», «membership» or «page».</summary>
        public string? HoldRead { get; set; }

        public TaskCompletionSource ReadHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ReadReached { get; private set; }

        public int NewListPageReads { get; private set; }

        /// <summary>Every page read, of any list; the membership walk's reads are not counted.</summary>
        public int PageReads { get; private set; }

        /// <summary>A page read fails, as the view's own read of the list on screen can.</summary>
        public bool PageThrows { get; set; }

        /// <summary>What every page read answers with; empty unless a test needs rows to act on.</summary>
        public IReadOnlyList<VariableListItem> PageItems { get; init; } = [];

        private async Task HoldIfAsync(string read)
        {
            // A list read waits once, so the next reader's sign-in can read; the new list's reads all wait, or a reload
            // the press set off would be held while the press's own read passed.
            if (HoldRead == read && !ReadHeld.Task.IsCompleted)
            {
                HoldRead = read == "lists" ? null : read;
                ReadReached = true;
                await ReadHeld.Task;
            }
        }

        public override async Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            MyListsCalls++;
            var lists = Lists.ToList();
            await HoldIfAsync("lists");
            return lists;
        }

        /// <summary>Thrown by every create, as the write limiter can.</summary>
        public Exception? CreateThrows { get; set; }

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            if (CreateThrows is { } thrown)
            {
                throw thrown;
            }

            Created.Add(name);
            var list = new VariableList { Id = Guid.NewGuid(), Name = name };
            Lists.Add(list);
            return Task.FromResult(list);
        }

        public Action? BeforeAdd { get; set; }
        public VariableDatasamlingKey? Stale { get; init; }

        /// <summary>The 400 names the stale row's variable, as Munin's body does.</summary>
        public bool NamesStale { get; init; }

        /// <summary>Munin's write limiter: every add after this many answers 429.</summary>
        public int? ThrottleAfter { get; set; }

        /// <summary>When set, every add waits for it: the round trip a test changes the reader during.</summary>
        public Task<bool>? HoldAdds { get; init; }

        public int AddCalls { get; private set; }

        public override async Task<bool> AddItemsToMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
            BeforeAdd?.Invoke();
            AddCalls++;

            if (HoldAdds is { } hold)
            {
                return await hold;
            }

            if (AddCalls > ThrottleAfter)
            {
                throw new MuninExplorerRateLimitedException();
            }

            if (Stale is { } stale && items.Contains(stale))
            {
                throw NamesStale
                    ? new ListItemsRefusedException([stale.VariableId])
                    : new HttpRequestException("stale datasamling", null, System.Net.HttpStatusCode.BadRequest);
            }

            if (AddThrows)
            {
                throw new HttpRequestException("nede");
            }

            Added.Add(items);
            return true;
        }

        /// <summary>Munin's write limiter refuses every delete, as it can the one after a refused add.</summary>
        public bool DeleteThrows { get; init; }

        public override Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (DeleteThrows)
            {
                throw new MuninExplorerRateLimitedException();
            }

            Lists.RemoveAll(l => l.Id == id);
            return Task.FromResult(true);
        }

        public override async Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            // The membership walk reads a thousand at a time; the view reads its page.
            if (id != OwnListId)
            {
                NewListPageReads += pageSize == 1000 ? 0 : 1;
                await HoldIfAsync(pageSize == 1000 ? "membership" : "page");
            }

            if (pageSize != 1000)
            {
                PageReads++;

                if (PageThrows)
                {
                    throw new HttpRequestException("nede");
                }
            }

            var items = pageSize == 1000 ? [] : PageItems;
            return new Page<VariableListItem> { Items = items, TotalCount = items.Count, PageNumber = 1, Size = pageSize, TotalPages = 1 };
        }

        public override Task<ExportedList> ExportItemsAsync(
            IReadOnlyCollection<VariableDatasamlingKey> items, Uri? explorerPageUrl = null,
            ExportFormat format = ExportFormat.Xlsx, bool includeKodeverk = false, CancellationToken cancellationToken = default)
        {
            Exported.Add(items);
            return Task.FromResult(new ExportedList([1], "application/octet-stream", "Variabelliste.xlsx"));
        }

        public SharedList? SharedAnswer { get; init; }

        public override Task<SharedList?> GetSharedListAsync(string? code, CancellationToken cancellationToken = default) =>
            Task.FromResult(SharedAnswer);

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VariableListView_WhenTheReaderChangesDuringTheCarryOver_ThenThePressLeavesNothingBehind(bool anotherSignsIn)
    {
        // The press belongs to the reader who made it: whoever is at the screen when it ends is told nothing and moved nowhere.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { HoldAdds = hold.Task };
        client.Lists.Add(new VariableList { Id = OwnListId, Name = "Mine hjertevariabler" });
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        var state = Services.GetRequiredService<VariableListState>();

        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Equal(1, client.AddCalls));
        var focusCalls = JSInterop.Invocations[Focus].Count;

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        if (anotherSignsIn)
        {
            cut.Render(p => p.Add(c => c.IsAuthenticated, true));
            cut.WaitForAssertion(() => Assert.Equal(OwnListId, state.ActiveListId));
        }

        var activeBefore = state.ActiveListId;
        hold.SetResult(true);
        await cut.InvokeAsync(() => Task.Delay(200));

        Assert.DoesNotContain(No.CarryBrowserListOverError, cut.Markup);
        Assert.DoesNotContain(No.BrowserListSavedAs(No.FirstListName), cut.Markup);
        Assert.Equal(activeBefore, state.ActiveListId);
        Assert.Equal(focusCalls, JSInterop.Invocations[Focus].Count);
        Assert.Single(browser.Rows);
        if (anotherSignsIn)
        {
            Assert.Contains("Mine hjertevariabler", cut.Find("[id^=munin-explorer-list-heading]").TextContent);
        }
    }

    [Fact]
    public void VariableListView_WhenMuninNamesAVariable_ThenNothingIsSavedAndTheButtonRemovesThatVariablesRows()
    {
        // Two rows of one variable, one of them from a datasamling no longer its own, and a row of another variable.
        var browser = new FakeBrowserList();
        var valid = FakeBrowserList.Row("Alder", Guid.NewGuid());
        var stale = valid with { DatasamlingId = Guid.NewGuid() };
        var other = FakeBrowserList.Row("Kjønn");
        browser.Rows.AddRange([valid, stale, other]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(stale), NamesStale = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(3), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        // All or nothing: the new list is gone again and every row stays; the sentence counts the named variable's rows.
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefused(2, 1), cut.Find("[role=alert]:not(:empty)").TextContent));
        Assert.DoesNotContain(No.CarryBrowserListOverError, cut.Markup);
        Assert.Empty(client.Lists);
        Assert.Empty(client.Added);
        Assert.Equal(3, browser.Rows.Count);
        var remove = ButtonSaying(cut, No.RemoveRefusedFromBrowser(2));
        cut.WaitForAssertion(() => JSInterop.Invocations[Focus][^1].Arguments[0].ShouldBeElementReferenceTo(remove));
        var focusesBefore = JSInterop.Invocations[Focus].Count;

        remove.Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefusedRemoved(2, 1), cut.Markup));
        Assert.Equal([VariableDatasamlingKey.Of(other)], browser.Rows.Select(VariableDatasamlingKey.Of));
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(2), cut.Markup);

        // The offer is back for the row that is left, and focus is on it.
        Assert.Contains(No.BrowserListWaiting(1), cut.Markup);
        cut.WaitForAssertion(() => Assert.Equal(focusesBefore + 1, JSInterop.Invocations[Focus].Count));

        // bUnit prints no reference on a button kept from an earlier render, so the view's own reference stands in for it.
        var offer = (ElementReference)typeof(VariableListView)
            .GetField("_carryOverButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(cut.Instance)!;
        Assert.Equal(offer.Id, ((ElementReference)JSInterop.Invocations[Focus][^1].Arguments[0]!).Id);

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListSavedAs(No.FirstListName), cut.Markup));
        Assert.Equal([VariableDatasamlingKey.Of(other)], Assert.Single(client.Added));
        Assert.Empty(browser.Rows);
    }

    [Fact]
    public void VariableListView_WhenTheRefusedRowsAreKept_ThenTheNextPressIsRefusedTheSameWay()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[0]), NamesStale = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefused(1, 1), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Equal(2, client.AddCalls));
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefused(1, 1), cut.Markup));
        Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup);
        Assert.Empty(client.Lists);
        Assert.Equal(2, browser.Rows.Count);
    }

    [Fact]
    public async Task VariableListView_WhenAnotherTabRemovesARefusedRow_ThenTheSentenceAndTheButtonCountTheSameRows()
    {
        // Counted from two sets, the sentence would say two rows while the button removes one.
        var browser = new FakeBrowserList();
        var valid = FakeBrowserList.Row("Alder", Guid.NewGuid());
        var stale = valid with { DatasamlingId = Guid.NewGuid() };
        browser.Rows.AddRange([valid, stale, FakeBrowserList.Row("Kjønn")]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(stale), NamesStale = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(3), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefused(2, 1), cut.Markup));

        browser.Rows.Remove(valid);
        await cut.InvokeAsync(browser.ChangedElsewhereAsync);

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefused(1, 1), cut.Markup));
        Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup);
        Assert.DoesNotContain(No.BrowserListRefused(2, 1), cut.Markup);
    }

    [Fact]
    public async Task VariableListView_WhenAnotherReaderSignsInDuringTheRemoval_ThenTheyAreToldNothingAndFocusStays()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[0]), NamesStale = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup));
        browser.Hold = true;

        ButtonSaying(cut, No.RemoveRefusedFromBrowser(1)).Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(browser.Pending));
        var focusCalls = JSInterop.Invocations[Focus].Count;
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        browser.Hold = false;
        while (browser.Pending.Count > 0)
        {
            await cut.InvokeAsync(browser.ReleaseNewest);
        }

        await cut.InvokeAsync(() => Task.Delay(200));

        Assert.Single(browser.Rows);
        Assert.DoesNotContain(No.BrowserListRefusedRemoved(1, 1), cut.Markup);
        Assert.DoesNotContain(No.BrowserListRefusedNotRemoved, cut.Markup);
        Assert.Equal(focusCalls, JSInterop.Invocations[Focus].Count);
    }

    [Fact]
    public void VariableListView_WhenAnotherReaderSignsInAfterARefusal_ThenTheyAreNotOfferedItsRemoval()
    {
        // The refusal was told to the reader who pressed; the next one has made no press to remove rows for.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[0]), NamesStale = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup));

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(1), cut.Markup);
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("removed")]
    [InlineData("saved")]
    [InlineData("failed")]
    public void VariableListView_WhenAnotherReaderSignsInAfterAPress_ThenNoneOfItsSentencesOrButtonsShow(string outcome)
    {
        // What a press said was said to the reader who pressed; the next one at the screen made no such press.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = outcome switch
        {
            "saved" => new Client(),
            "failed" => new Client { AddThrows = true },
            _ => new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[0]), NamesStale = true },
        };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        var said = outcome switch
        {
            "saved" => No.BrowserListSavedAs(No.FirstListName),
            "failed" => No.CarryBrowserListOverError,
            _ => No.BrowserListRefused(1, 1),
        };
        cut.WaitForAssertion(() => Assert.Contains(said, cut.Markup));
        if (outcome == "removed")
        {
            browser.Failing.Add("removeKeys");
            ButtonSaying(cut, No.RemoveRefusedFromBrowser(1)).Click();
            cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefusedNotRemoved, cut.Markup));
            browser.Failing.Clear();
        }

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        if (outcome != "saved")
        {
            cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        }

        Assert.DoesNotContain(No.BrowserListRefused(1, 1), cut.Markup);
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(1), cut.Markup);
        Assert.DoesNotContain(No.BrowserListRefusedNotRemoved, cut.Markup);
        Assert.DoesNotContain(No.BrowserListSavedAs(No.FirstListName), cut.Markup);
        Assert.DoesNotContain(No.CarryBrowserListOverError, cut.Markup);
    }

    [Fact]
    public async Task VariableListView_WhenTheReaderOpensAFormWhileTheCarryOverIsRefused_ThenFocusStaysInTheForm()
    {
        // The refusal's button must not take focus from a field the reader moved to while the press was out.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { HoldAdds = hold.Task };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Equal(1, client.AddCalls));

        cut.Find("button[id^='munin-explorer-create-toggle-']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input.munin-explorer-list-create__field")));
        await cut.InvokeAsync(() => Task.Delay(100));
        var focusCalls = JSInterop.Invocations[Focus].Count;
        hold.SetException(new ListItemsRefusedException([browser.Rows[0].VariableId]));

        cut.WaitForAssertion(() => Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup));
        await cut.InvokeAsync(() => Task.Delay(100));

        Assert.Equal(focusCalls, JSInterop.Invocations[Focus].Count);
    }

    [Fact]
    public void VariableListView_WhenASharedListIsOpenedAfterARefusal_ThenTheRemoveButtonIsNotDrawnOverIt()
    {
        // The shared list's rows are not the browser's: a button there would read as removing them. The view's
        // shared-list branch draws no offer at all, which is what holds this, not a check of its own.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = new Client
        {
            Stale = VariableDatasamlingKey.Of(browser.Rows[0]),
            NamesStale = true,
            SharedAnswer = new SharedList("Delt liste", [Stored(Alder)]),
        };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup));

        cut.Render(p => p.Add(c => c.ShareCode, "ABC123"));

        cut.WaitForAssertion(() => Assert.Contains("Delt liste", cut.Markup));
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(1), cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenTheRefusedRowsCannotBeRemoved_ThenTheButtonStaysToTryAgain()
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        browser.Failing.Add("removeKeys");
        var client = new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[0]), NamesStale = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefused(1, 1), cut.Markup));

        ButtonSaying(cut, No.RemoveRefusedFromBrowser(1)).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefusedNotRemoved, cut.Markup));
        Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup);
        Assert.Equal(2, browser.Rows.Count);
    }

    [Fact]
    public void VariableListView_WhenTheNextPressFailsForAnotherReason_ThenTheRefusalAndItsButtonAreGone()
    {
        // A press starts afresh: the button belonged to the refusal before it, which this press did not repeat.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[0]), NamesStale = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup));
        client.ThrottleAfter = 1;

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.CarryBrowserListOverError, cut.Markup));
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(1), cut.Markup);
        Assert.DoesNotContain(No.BrowserListRefused(1, 1), cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenTheRefusedRowsStayInTheStoredValue_ThenItSaysTheyWereNotRemoved()
    {
        var browser = new FakeBrowserList { KeepsStored = true };
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[0]), NamesStale = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefused(1, 1), cut.Markup));

        ButtonSaying(cut, No.RemoveRefusedFromBrowser(1)).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefusedNotRemoved, cut.Markup));
        Assert.DoesNotContain(No.BrowserListRefusedRemoved(1, 1), cut.Markup);
    }

    [Theory]
    [InlineData("unnamed")]
    [InlineData("throttled")]
    public void VariableListView_WhenTheAddFailsWithoutNamingARow_ThenNothingIsSavedAndTheGeneralSentenceShows(string failure)
    {
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Alder"), FakeBrowserList.Row("Gammel")]);
        var client = failure == "unnamed"
            ? new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[1]) }
            : new Client { ThrottleAfter = 0 };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.CarryBrowserListOverError, cut.Markup));
        Assert.Single(client.Created);
        Assert.Empty(client.Lists);
        Assert.Equal(2, browser.Rows.Count);
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(1), cut.Markup);
        Assert.DoesNotContain(No.BrowserListSavedAs(No.FirstListName), cut.Markup);
    }

    [Theory]
    [InlineData("lists")]
    [InlineData("membership")]
    [InlineData("page")]
    public async Task VariableListView_WhenAnotherReaderSignsInDuringTheCarryOversReads_ThenThePressLeavesThemNothing(string read)
    {
        // One per await in the press's tail: the counts, the new list's membership, and its page.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client();
        client.Lists.Add(new VariableList { Id = OwnListId, Name = "Mine hjertevariabler" });
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        var state = Services.GetRequiredService<VariableListState>();
        cut.WaitForAssertion(() => Assert.Equal(OwnListId, state.ActiveListId));
        client.HoldRead = read;

        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.True(client.ReadReached));
        var focusCalls = JSInterop.Invocations[Focus].Count;

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForAssertion(() => Assert.NotNull(state.ActiveListId));
        var activeBefore = state.ActiveListId;
        var pageReads = client.NewListPageReads;
        client.ReadHeld.SetResult();
        await cut.InvokeAsync(() => Task.Delay(200));

        Assert.DoesNotContain(No.BrowserListSavedAs(No.FirstListName), cut.Markup);
        Assert.Equal(activeBefore, state.ActiveListId);
        Assert.Equal(focusCalls, JSInterop.Invocations[Focus].Count);

        // Nor is the old reader's new list read for a page under the next reader.
        Assert.Equal(pageReads, client.NewListPageReads);
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

    // Two rows of one variable Munin names, and one row of another.
    private static (FakeBrowserList Browser, Client Client) NamedRefusal(bool deleteThrows = false, bool onlyRefused = false)
    {
        var browser = new FakeBrowserList();
        var valid = FakeBrowserList.Row("Alder", Guid.NewGuid());
        var stale = valid with { DatasamlingId = Guid.NewGuid() };
        browser.Rows.AddRange([valid, stale]);
        if (!onlyRefused)
        {
            browser.Rows.Add(FakeBrowserList.Row("Kjønn"));
        }

        return (browser, new Client { Stale = VariableDatasamlingKey.Of(stale), NamesStale = true, DeleteThrows = deleteThrows });
    }

    private IRenderedComponent<VariableListView> RefusedView(FakeBrowserList browser, Client client)
    {
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(browser.Rows.Count), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefused(2, 1), cut.Markup));
        return cut;
    }

    [Fact]
    public void VariableListView_WhenAListIsCreatedAfterARefusal_ThenTheRemoveButtonLeavesWithItsSentence()
    {
        // Without the sentence beside it, the button would offer to remove rows nobody has just been told about.
        var (browser, client) = NamedRefusal();
        var cut = RefusedView(browser, client);

        cut.Find("button[id^='munin-explorer-create-toggle-']").Click();
        cut.Find("input[id^='munin-explorer-new-list-']").Change("Hjerte og kar");
        ButtonSaying(cut, "Opprett liste").Click();

        cut.WaitForAssertion(() => Assert.Contains("Hjerte og kar", client.Created));
        cut.WaitForAssertion(() => Assert.DoesNotContain(No.BrowserListRefused(2, 1), cut.Markup));
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(2), cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenAListIsCreatedAfterAFailedCarryOver_ThenTheFailureSentenceLeaves()
    {
        // Left standing, the carry-over's failure would read as the answer to the create the reader just made.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client { AddThrows = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.CarryBrowserListOverError, cut.Markup));

        cut.Find("button[id^='munin-explorer-create-toggle-']").Click();
        cut.Find("input[id^='munin-explorer-new-list-']").Change("Hjerte og kar");
        ButtonSaying(cut, "Opprett liste").Click();

        cut.WaitForAssertion(() => Assert.Contains("Hjerte og kar", client.Created));
        cut.WaitForAssertion(() => Assert.DoesNotContain(No.CarryBrowserListOverError, cut.Markup));
    }

    [Fact]
    public void VariableListView_WhenTheFirstReadFindsNoBrowserForAMoment_ThenALaterRenderShowsTheRows()
    {
        // A dropped circuit or a timeout is not the host's answer for good; cached, the list would be gone for the circuit.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        browser.Failing.Add("count");
        var cut = ListView(new Client(), browser);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListUnavailable, cut.Markup));

        browser.Failing.Clear();
        cut.Render();

        cut.WaitForAssertion(() => Assert.Equal(["Alder"], RowNames(cut)));
        Assert.DoesNotContain(No.BrowserListUnavailable, cut.Markup);
    }

    [Fact]
    public async Task VariableListView_WhenAnotherReaderSignsInDuringTheRemovalAndIsToldSomething_ThenTheRemovalLeavesItStanding()
    {
        // Pins the reader guard after the removal's await: past it, ForgetFailures would empty the next reader's alert.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[0]), NamesStale = true };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.RemoveRefusedFromBrowser(1), cut.Markup));
        browser.Hold = true;
        ButtonSaying(cut, No.RemoveRefusedFromBrowser(1)).Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(browser.Pending));

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        client.CreateThrows = new MuninExplorerRateLimitedException();
        cut.Find("button[id^='munin-explorer-create-toggle-']").Click();
        cut.Find("input[id^='munin-explorer-new-list-']").Change("Hjerte og kar");
        ButtonSaying(cut, "Opprett liste").Click();
        cut.WaitForAssertion(() => Assert.Contains(No.RateLimitError, cut.Markup));
        browser.Hold = false;
        while (browser.Pending.Count > 0)
        {
            await cut.InvokeAsync(browser.ReleaseNewest);
        }

        await cut.InvokeAsync(() => Task.Delay(200));

        Assert.Single(browser.Rows);
        Assert.Contains(No.RateLimitError, cut.Markup);
        Assert.DoesNotContain(No.BrowserListRefusedRemoved(1, 1), cut.Markup);
    }

    [Fact]
    public async Task VariableListView_WhenARemovePressArrivesAfterAnotherReaderSignedIn_ThenNoRowLeaves()
    {
        // Pins the handler's entry guard. Blazor Server keeps a handler until the browser acknowledges the render that
        // drops its button, so a press sent before the sign-in reached the screen still arrives; reflection stands in.
        var (browser, client) = NamedRefusal();
        var cut = RefusedView(browser, client);

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        var press = typeof(VariableListView).GetMethod(
            "RemoveRefusedFromBrowserAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await cut.InvokeAsync(() => (Task)press.Invoke(cut.Instance, null)!);

        Assert.Equal(3, browser.Rows.Count);
        Assert.DoesNotContain(browser.Calls, c => c.Call == "removeKeys");
    }

    [Fact]
    public async Task VariableListView_WhenAnotherReaderSignsInWhileTheSavedRowsLeaveTheBrowser_ThenThePressReadsNothingForThem()
    {
        // Pins the reader guard after the carry-over's await: the state hands the list back here, and past the guard the
        // press would read the lists again under the next reader.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client();
        client.BeforeAdd = () => browser.Hold = true;
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(browser.Pending, p => p.Call == "removeKeys"));

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        await cut.InvokeAsync(() => Task.Delay(100));
        var listReads = client.MyListsCalls;
        browser.Hold = false;
        while (browser.Pending.Count > 0)
        {
            await cut.InvokeAsync(browser.ReleaseNewest);
        }

        await cut.InvokeAsync(() => Task.Delay(200));

        Assert.Equal(listReads, client.MyListsCalls);
        Assert.DoesNotContain(No.BrowserListSavedAs(No.FirstListName), cut.Markup);
    }

    [Fact]
    public void VariableListView_WhenAnOfferFocusNamesNoControlItKnows_ThenItThrows()
    {
        // A control added to the enum and not to the switch would otherwise send focus down the carry-over's branch.
        var cut = ListView(new Client(), new FakeBrowserList(), signedIn: true);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var field = typeof(VariableListView).GetField("_offerFocus", flags)!;
        var tuple = Nullable.GetUnderlyingType(field.FieldType)!;
        var control = Enum.ToObject(tuple.GetGenericArguments()[0], 99);
        var reader = Services.GetRequiredService<VariableListState>().Reader;
        field.SetValue(cut.Instance, Activator.CreateInstance(tuple, control, 0, reader));

        var take = typeof(VariableListView).GetMethod("TakeOfferFocusTarget", flags)!;
        var thrown = Assert.Throws<System.Reflection.TargetInvocationException>(() => take.Invoke(cut.Instance, null));

        Assert.IsType<System.Diagnostics.UnreachableException>(thrown.InnerException);
    }

    [Fact]
    public void VariableListView_WhenTheRemoveButtonIsDrawn_ThenItIsDescribedByTheSentenceNamingTheRowsAndThisBrowser()
    {
        // «Fjern disse radene» alone does not say which rows or where; the sentence it points at does.
        var (browser, client) = NamedRefusal();
        browser.Failing.Add("removeKeys");
        var cut = RefusedView(browser, client);

        var described = ButtonSaying(cut, No.RemoveRefusedFromBrowser(2)).GetAttribute("aria-describedby");
        Assert.Equal(No.BrowserListRefused(2, 1), cut.Find($"#{described}").TextContent.Trim());

        ButtonSaying(cut, No.RemoveRefusedFromBrowser(2)).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefusedNotRemoved, cut.Markup));
        described = ButtonSaying(cut, No.RemoveRefusedFromBrowser(2)).GetAttribute("aria-describedby");
        Assert.Equal(No.BrowserListRefusedNotRemoved, cut.Find($"#{described}").TextContent.Trim());
    }

    [Fact]
    public void VariableListView_WhenTheDeleteAfterANamedRefusalFails_ThenTheRefusalStandsAndTheLeftListIsKeptAmongTheReadersLists()
    {
        // The delete's own failure is not the reader's news: the refusal and its button are. The list is drawn where
        // the reader's lists are next drawn, and deleted there.
        var (browser, client) = NamedRefusal(deleteThrows: true);
        var cut = RefusedView(browser, client);

        Assert.Contains(No.RemoveRefusedFromBrowser(2), cut.Markup);
        Assert.DoesNotContain(No.CarryBrowserListOverError, cut.Markup);
        var left = Assert.Single(client.Lists);
        Assert.Contains(Services.GetRequiredService<VariableListState>().Lists, l => l.Id == left.Id);
        Assert.Equal(3, browser.Rows.Count);
    }

    [Fact]
    public void VariableListView_WhenEveryRefusedRowIsRemoved_ThenThereIsNoOfferAndFocusGoesToCreatingAList()
    {
        var (browser, client) = NamedRefusal(onlyRefused: true);
        var cut = RefusedView(browser, client);
        var focusesBefore = JSInterop.Invocations[Focus].Count;

        ButtonSaying(cut, No.RemoveRefusedFromBrowser(2)).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefusedRemoved(2, 0), cut.Markup));
        Assert.Empty(browser.Rows);
        Assert.DoesNotContain(No.CarryBrowserListOver, cut.Markup);

        // The toggle's reference is not printed by bUnit (see above), so the call is compared with the view's own.
        cut.WaitForAssertion(() => Assert.Equal(focusesBefore + 1, JSInterop.Invocations[Focus].Count));
        var toggle = (ElementReference)typeof(VariableListView)
            .GetField("_createToggle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(cut.Instance)!;
        Assert.Equal(toggle.Id, ((ElementReference)JSInterop.Invocations[Focus][^1].Arguments[0]!).Id);
    }

    [Fact]
    public async Task VariableListView_WhenAnotherTabRemovesEveryRefusedRow_ThenTheSentenceKeepsThePressCountAndTheButtonGoes()
    {
        // Counted from the rows still here, the sentence would read «0 rader»; the press's own count is what Munin refused.
        var (browser, client) = NamedRefusal();
        var cut = RefusedView(browser, client);

        browser.Rows.RemoveRange(0, 2);
        await cut.InvokeAsync(browser.ChangedElsewhereAsync);

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        Assert.Contains(No.BrowserListRefused(2, 1), cut.Markup);
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(2), cut.Markup);
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(1), cut.Markup);
    }

    [Fact]
    public async Task VariableListView_WhenTheCarryOverIsPressedWhileARemovalIsOut_ThenNothingIsSent()
    {
        // The press would read the rows the removal is taking out and send them to Munin as they leave.
        var (browser, client) = NamedRefusal();
        var cut = RefusedView(browser, client);
        browser.Hold = true;

        ButtonSaying(cut, No.RemoveRefusedFromBrowser(2)).Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(browser.Pending));
        Assert.Equal("true", ButtonSaying(cut, No.CarryBrowserListOver).GetAttribute("aria-disabled"));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        browser.Hold = false;
        while (browser.Pending.Count > 0)
        {
            await cut.InvokeAsync(browser.ReleaseNewest);
        }

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefusedRemoved(2, 1), cut.Markup));
        await cut.InvokeAsync(() => Task.Delay(100));
        Assert.Single(client.Created);
        Assert.Equal(1, client.AddCalls);
    }

    private static ElementReference Field(IRenderedComponent<VariableListView> cut, string name) =>
        (ElementReference)typeof(VariableListView)
            .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(cut.Instance)!;

    [Fact]
    public async Task VariableListView_WhenAPageReadFailsUnderARefusal_ThenTheRemoveButtonAndItsSentenceIdLeave()
    {
        // The alert then says the read failed; the button would be described by that sentence and remove rows it never named.
        var (browser, client) = NamedRefusal();
        client.Lists.Add(new VariableList { Id = OwnListId, Name = "Mine hjertevariabler" });
        var cut = RefusedView(browser, client);
        Assert.Contains(No.RemoveRefusedFromBrowser(2), cut.Markup);
        client.PageThrows = true;

        await cut.InvokeAsync(() => Services.GetRequiredService<VariableListState>().RefreshAsync());

        cut.WaitForAssertion(() => Assert.Contains(No.ListLoadError, cut.Markup));
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(2), cut.Markup);
        Assert.Empty(cut.FindAll("[id^=munin-explorer-browser-list-refusal-]"));
    }

    [Fact]
    public void VariableListView_WhenADownloadFailsAfterASavedCarryOver_ThenTheDownloadsSentenceShows()
    {
        // The saved sentence answered the press before; left standing, it would answer for the download too.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client { PageItems = [Stored(Alder)] };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListSavedAs(No.FirstListName), cut.Markup));

        cut.WaitForAssertion(() => cut.Find("button[id^=munin-explorer-list-download-toggle]").Click());
        cut.FindAll("[id^=munin-explorer-list-download-] button.munin-explorer-list-menu__item")[0].Click();

        cut.WaitForAssertion(() => Assert.Contains(No.DownloadError, cut.Markup));
        Assert.DoesNotContain(No.BrowserListSavedAs(No.FirstListName), cut.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VariableListView_WhenAFailedPressIsFollowedByARefusal_ThenOnlyTheRefusalIsSaid(bool anotherReaderPresses)
    {
        // Each press says its own outcome: the earlier failure, the same reader's or the last reader's, is not this one's.
        var browser = new FakeBrowserList();
        browser.Rows.AddRange([FakeBrowserList.Row("Gammel"), FakeBrowserList.Row("Alder")]);
        var client = new Client { Stale = VariableDatasamlingKey.Of(browser.Rows[0]), NamesStale = true, ThrottleAfter = 0 };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Contains(No.CarryBrowserListOverError, cut.Markup));
        client.ThrottleAfter = null;
        if (anotherReaderPresses)
        {
            cut.Render(p => p.Add(c => c.IsAuthenticated, false));
            cut.Render(p => p.Add(c => c.IsAuthenticated, true));
            cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(2), cut.Markup));
        }

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefused(1, 1), cut.Markup));
        Assert.DoesNotContain(No.CarryBrowserListOverError, cut.Markup);
    }

    [Theory]
    [InlineData("throttled", LogLevel.Warning)]
    [InlineData("unauthorised", LogLevel.Warning)]
    [InlineData("failed", LogLevel.Error)]
    public void VariableListView_WhenTheCarryOverFails_ThenALimiterOrSignInRefusalIsAWarningAndAFaultAnError(string failure, LogLevel level)
    {
        // One catch folds the three into one sentence; the level is what still tells throttling apart from a fault.
        var recorder = new RecordingLoggerProvider();
        Services.AddLogging(b => b.AddProvider(recorder).SetMinimumLevel(LogLevel.Trace));
        Exception thrown = failure switch
        {
            "throttled" => new MuninExplorerRateLimitedException(),
            "unauthorised" => new MuninExplorerUnauthorizedException(),
            _ => new HttpRequestException("nede"),
        };
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var cut = ListView(new Client { HoldAdds = Task.FromException<bool>(thrown) }, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.CarryBrowserListOverError, cut.Markup));
        var entry = Assert.Single(recorder.Entries, e => e.Category == typeof(VariableListView).FullName && e.Exception == thrown);
        Assert.Equal(level, entry.Level);
    }

    [Fact]
    public async Task VariableListView_WhenAnotherReaderSignsInWhileTheNewListIsMadeActive_ThenNothingIsReadForThem()
    {
        // The press stops at the reader change rather than reading a page that is the next reader's to ask for.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var client = new Client();
        client.Lists.Add(new VariableList { Id = OwnListId, Name = "Mine hjertevariabler" });
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        var state = Services.GetRequiredService<VariableListState>();
        cut.WaitForAssertion(() => Assert.Equal(OwnListId, state.ActiveListId));
        client.HoldRead = "membership";

        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.True(client.ReadReached));
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForAssertion(() => Assert.Equal(OwnListId, state.ActiveListId));
        await cut.InvokeAsync(() => Task.Delay(100));
        var pageReads = client.PageReads;
        client.ReadHeld.SetResult();
        await cut.InvokeAsync(() => Task.Delay(200));

        Assert.Equal(pageReads, client.PageReads);
    }

    [Fact]
    public async Task VariableListView_WhenTheReaderOpensAFormWhileTheCarryOverSaves_ThenFocusStaysInTheForm()
    {
        // The saved list's focus target must not take focus from a field the reader moved to while the press was out.
        var browser = new FakeBrowserList();
        browser.Rows.Add(FakeBrowserList.Row("Alder"));
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { HoldAdds = hold.Task };
        var cut = ListView(client, browser, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));
        ButtonSaying(cut, No.CarryBrowserListOver).Click();
        cut.WaitForAssertion(() => Assert.Equal(1, client.AddCalls));

        cut.Find("button[id^='munin-explorer-create-toggle-']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input.munin-explorer-list-create__field")));
        await cut.InvokeAsync(() => Task.Delay(100));
        var focusCalls = JSInterop.Invocations[Focus].Count;
        hold.SetResult(true);

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListSavedAs(No.FirstListName), cut.Markup));
        await cut.InvokeAsync(() => Task.Delay(100));

        Assert.Equal(focusCalls, JSInterop.Invocations[Focus].Count);
    }

    [Fact]
    public void VariableListView_WhenTheRefusedRowsLeaveThePageButStayStored_ThenFocusGoesToTheOfferAsTheButtonLeaves()
    {
        // The button the reader pressed is gone with the page's rows; left unmoved, focus falls to the document.
        var (browser, client) = NamedRefusal();
        browser.KeepsStored = true;
        var cut = RefusedView(browser, client);
        var focusesBefore = JSInterop.Invocations[Focus].Count;

        ButtonSaying(cut, No.RemoveRefusedFromBrowser(2)).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefusedNotRemoved, cut.Markup));
        Assert.DoesNotContain(No.RemoveRefusedFromBrowser(2), cut.Markup);
        cut.WaitForAssertion(() => Assert.Equal(focusesBefore + 1, JSInterop.Invocations[Focus].Count));
        Assert.Equal(Field(cut, "_carryOverButton").Id, ((ElementReference)JSInterop.Invocations[Focus][^1].Arguments[0]!).Id);
    }

    [Fact]
    public void VariableListView_WhenEveryRefusedRowIsRemovedWithListsToChooseFrom_ThenFocusGoesToThePicker()
    {
        var (browser, client) = NamedRefusal(onlyRefused: true);
        client.Lists.Add(new VariableList { Id = OwnListId, Name = "Mine hjertevariabler" });
        client.Lists.Add(new VariableList { Id = Guid.NewGuid(), Name = "Kreft" });
        var cut = RefusedView(browser, client);
        var focusesBefore = JSInterop.Invocations[Focus].Count;

        ButtonSaying(cut, No.RemoveRefusedFromBrowser(2)).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListRefusedRemoved(2, 0), cut.Markup));
        cut.WaitForAssertion(() => Assert.Equal(focusesBefore + 1, JSInterop.Invocations[Focus].Count));
        Assert.Equal(Field(cut, "_listPicker").Id, ((ElementReference)JSInterop.Invocations[Focus][^1].Arguments[0]!).Id);
    }
}
