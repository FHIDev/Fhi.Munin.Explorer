using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A signed-out reader's variable list, kept in their browser when the host turns
/// <c>MuninExplorer:Features:AnonymousLists</c> on (Fhi.Metadata-emsc0.3.12).
/// </summary>
public class AnonymousListTest : ExplorerTestContext
{
    private const string Read = "readAnonymousList";
    private const string Write = "writeAnonymousList";

    private static readonly Texts No = Texts.For("no");

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

    private static string Stored(params (Guid Variable, Guid? Datasamling, string Name)[] rows) =>
        JsonSerializer.Serialize(new
        {
            v = 1,
            name = "Min variabelliste",
            items = rows.Select(r => new { variabelId = r.Variable, datasamlingId = r.Datasamling, variabelName = r.Name, variabelCode = r.Name }),
        });

    private sealed class Client(params VariableSummary[] rows) : EmptyMuninExplorerClient
    {
        public int MyListsCalls { get; private set; }
        public List<IReadOnlyCollection<VariableDatasamlingKey>> Exported { get; } = [];
        public List<(string Name, IReadOnlyCollection<VariableListItem> Items)> Shared { get; } = [];
        public List<string> Created { get; } = [];
        public List<IReadOnlyCollection<VariableDatasamlingKey>> Added { get; } = [];
        public List<VariableList> Lists { get; } = [];
        public bool AddThrows { get; init; }

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

        public override Task<bool> AddItemsToMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
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
            return Task.FromResult("ABC123");
        }
    }

    /// <summary>The package's module, answering the two storage calls; <paramref name="writeFails"/> throws as a full quota does.</summary>
    private BunitJSModuleInterop Browser(string? stored = null, bool kept = true, Exception? writeFails = null, bool writeKept = true)
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<StoredBrowserList>(Read).SetResult(new StoredBrowserList(kept, stored));
        var write = module.Setup<bool>(Write, _ => true);

        if (writeFails is null)
        {
            write.SetResult(writeKept);
        }
        else
        {
            write.SetException(writeFails);
        }

        return module;
    }

    private void Register(Client client, bool? flag)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        if (flag is { } on)
        {
            Services.AddSingleton(new AnonymousListsSwitch(on));
        }
    }

    private IRenderedComponent<VariableSearch> Search(Client client, bool? flag, bool signedIn = false)
    {
        Register(client, flag);
        RenderFragment list = b => b.AddContent(0, "the list");
        return Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, signedIn).Add(c => c.VariableList, list));
    }

    private IRenderedComponent<VariableListView> ListView(Client client, bool signedIn = false)
    {
        Register(client, flag: true);
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
    public void Search_WhenTheFlagIsOff_ThenASignedOutReaderHasNoSaveButtonNoTabsAndNoStorageCall(bool? flag)
    {
        // Off is the default, and a host that has not opted in must see the signed-out page it always had.
        var module = Browser();
        var cut = Search(new Client(Alder), flag);

        cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click(new MouseEventArgs { Detail = 1 });

        Assert.Empty(cut.FindAll(".munin-explorer-detail button[aria-pressed]"));
        Assert.Empty(cut.FindAll("[role=tablist]"));
        Assert.Contains(No.SignInForVariableLists, cut.Markup);
        Assert.Empty(module.Invocations[Read]);
    }

    [Fact]
    public void ListView_WhenTheFlagIsOff_ThenASignedOutReaderGetsNothing()
    {
        Browser(Stored((Guid.NewGuid(), null, "Alder")));
        Register(new Client(), flag: false);

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, false));

        Assert.Equal("", cut.Markup.Trim());
    }

    // -----------------------------------------------------------------------
    // Adding and removing

    [Fact]
    public void Search_WhenTheFlagIsOn_ThenASignedOutReaderGetsTheListTab()
    {
        Browser();
        var cut = Search(new Client(Alder), flag: true);

        Assert.Single(cut.FindAll("[role=tablist]"));
        Assert.DoesNotContain(No.SignInForVariableLists, cut.Markup);
    }

    [Fact]
    public void Save_WhenSignedOutWithTheFlagOn_ThenTheRowGoesToTheBrowserAndNothingToMunin()
    {
        var module = Browser();
        var client = new Client(Alder);
        var cut = Search(client, flag: true);

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed")));
        Assert.Equal(0, client.MyListsCalls);
        Assert.Empty(client.Created);

        // Catalogue fields, the row's datasamling among them, and nothing the reader wrote.
        var json = (string)module.Invocations[Write][^1].Arguments[0]!;
        using var stored = JsonDocument.Parse(json);
        Assert.Equal(1, stored.RootElement.GetProperty("v").GetInt32());
        var item = Assert.Single(stored.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(Alder.Id, item.GetProperty("variabelId").GetGuid());
        Assert.Equal(Alder.DatasamlingId, item.GetProperty("datasamlingId").GetGuid());
        Assert.Equal("V_ALDER", item.GetProperty("variabelCode").GetString());
        Assert.False(item.TryGetProperty("notes", out _));
        Assert.False(item.TryGetProperty("desiredDataFreeText", out _));
        Assert.False(item.TryGetProperty("desiredDataCodes", out _));
    }

    [Fact]
    public void Save_WhenTheRowIsAlreadyInTheBrowserList_ThenThePressTakesItOutAndForgetsTheKey()
    {
        var module = Browser(Stored((Alder.Id, Alder.DatasamlingId, "Alder")));
        var cut = Search(new Client(Alder), flag: true);
        cut.WaitForAssertion(() => Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed")));

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed")));
        Assert.Null(module.Invocations[Write][^1].Arguments[0]);
    }

    [Fact]
    public void Save_WhenTheBrowserListHoldsTwoThousandRows_ThenTheRowIsRefusedWithTheReason()
    {
        // The anonymous export's own ceiling: a list it could not download is not a list worth growing.
        var full = Enumerable.Range(0, VariableListState.MaxBrowserListRows)
            .Select(n => (Guid.NewGuid(), (Guid?)null, $"V{n}")).ToArray();
        var module = Browser(Stored(full));
        var cut = Search(new Client(Alder), flag: true);
        cut.WaitForState(() => module.Invocations[Read].Count > 0);

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal(No.BrowserListFull(2000), Status(cut)));
        Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Empty(module.Invocations[Write]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Save_WhenTheBrowserRefusesStorage_ThenTheRowIsKeptForTheVisitAndTheReaderIsTold(bool scriptThrows)
    {
        // Both refusals: the script's own catch answers false, and a fault in it throws across the circuit.
        if (scriptThrows)
        {
            Browser(writeFails: new JSException("QuotaExceededError"));
        }
        else
        {
            Browser(writeKept: false);
        }

        var cut = Search(new Client(Alder), flag: true);

        SaveButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal(No.BrowserListNotKept, Status(cut)));
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
    }

    // -----------------------------------------------------------------------
    // Showing it

    [Fact]
    public void ListView_WhenABrowserListIsStored_ThenItIsReadBackOnMountWithTheSentences()
    {
        Browser(Stored((Guid.NewGuid(), null, "Alder ved diagnose"), (Guid.NewGuid(), null, "Kjønn")));

        var cut = ListView(new Client());

        cut.WaitForAssertion(() => Assert.Equal(["Alder ved diagnose", "Kjønn"], RowNames(cut)));
        Assert.Contains(No.BrowserListKeptHere, cut.Markup);
        Assert.Contains(No.BrowserListApplication, cut.Markup);
        Assert.Contains(No.BrowserListSignInFor, cut.Markup);
        Assert.DoesNotContain(No.BrowserListNotKept, cut.Markup);
    }

    [Fact]
    public void ListView_WhenTheBrowserCannotStore_ThenTheViewSaysTheListIsOnlyForThisVisit()
    {
        Browser(kept: false);

        var cut = ListView(new Client());

        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListNotKept, cut.Markup));
        Assert.DoesNotContain(No.BrowserListKeptHere, cut.Markup);
    }

    [Fact]
    public void Download_WhenPressed_ThenTheBrowserListsRowsGoToTheAnonymousExport()
    {
        var variable = Guid.NewGuid();
        var datasamling = Guid.NewGuid();
        Browser(Stored((variable, datasamling, "Alder")));
        var client = new Client();
        var cut = ListView(client);
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        cut.Find("button[id^=munin-explorer-list-download-toggle]").Click();
        cut.FindAll("[id^=munin-explorer-list-download-] button.munin-explorer-list-menu__item")[0].Click();

        cut.WaitForAssertion(() => Assert.Equal(new VariableDatasamlingKey(variable, datasamling), Assert.Single(Assert.Single(client.Exported))));
    }

    [Fact]
    public void Share_WhenPressed_ThenTheRowsGoToTheAnonymousShareAndTheCodeIsShown()
    {
        Browser(Stored((Guid.NewGuid(), null, "Alder")));
        var client = new Client();
        var cut = ListView(client);
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        ButtonSaying(cut, No.ShareList).Click();

        cut.WaitForAssertion(() => Assert.Equal("ABC123", cut.Find("input[id^=munin-explorer-share-code]").GetAttribute("value")));
        var (name, items) = Assert.Single(client.Shared);
        Assert.Equal("Min variabelliste", name);
        Assert.Single(items);
    }

    [Fact]
    public void Clear_WhenPressedOnce_ThenItOnlyAsksAndTheSecondStepEmptiesTheListAndTheKey()
    {
        var module = Browser(Stored((Guid.NewGuid(), null, "Alder")));
        var cut = ListView(new Client());
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        ButtonSaying(cut, No.ClearBrowserList).Click();

        Assert.Contains(No.ConfirmClearBrowserList, cut.Markup);
        Assert.Single(RowNames(cut));
        Assert.Empty(module.Invocations[Write]);

        ButtonSaying(cut, No.ConfirmEmptyYes).Click();

        cut.WaitForAssertion(() => Assert.Empty(RowNames(cut)));
        Assert.Null(Assert.Single(module.Invocations[Write]).Arguments[0]);
        Assert.Contains(No.EmptyList, cut.Markup);

        // Refused rather than gone, so the press that emptied it leaves focus somewhere.
        Assert.Equal("true", ButtonSaying(cut, No.ClearBrowserList).GetAttribute("aria-disabled"));
    }

    // -----------------------------------------------------------------------
    // Carry-over at sign-in

    [Fact]
    public void CarryOver_WhenSignedInWithABrowserList_ThenOnePressSavesItAsANewListAndEmptiesTheBrowserCopy()
    {
        var variable = Guid.NewGuid();
        var datasamling = Guid.NewGuid();
        var module = Browser(Stored((variable, datasamling, "Alder")));
        var client = new Client();
        client.Lists.Add(new VariableList { Id = Guid.NewGuid(), Name = "Min variabelliste" });
        var cut = ListView(client, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));

        // Never on its own: nothing is made until the reader presses.
        Assert.Empty(client.Created);

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.DoesNotContain(No.BrowserListWaiting(1), cut.Markup));
        Assert.Equal(["Min variabelliste (2)"], client.Created);
        Assert.Equal(new VariableDatasamlingKey(variable, datasamling), Assert.Single(Assert.Single(client.Added)));
        Assert.Null(module.Invocations[Write][^1].Arguments[0]);
    }

    [Fact]
    public void CarryOver_WhenTheApiFails_ThenTheBrowserListStaysAndTheReaderIsTold()
    {
        var module = Browser(Stored((Guid.NewGuid(), null, "Alder")));
        var client = new Client { AddThrows = true };
        var cut = ListView(client, signedIn: true);
        cut.WaitForAssertion(() => Assert.Contains(No.BrowserListWaiting(1), cut.Markup));

        ButtonSaying(cut, No.CarryBrowserListOver).Click();

        cut.WaitForAssertion(() => Assert.Contains(No.CarryBrowserListOverError, cut.Markup));
        Assert.Contains(No.BrowserListWaiting(1), cut.Markup);
        Assert.Empty(module.Invocations[Write]);

        // The half-made list is taken away again rather than left empty among the reader's own.
        Assert.Empty(client.Lists);
    }
}
