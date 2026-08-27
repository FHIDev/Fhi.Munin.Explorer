using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The saved-list view: what is in the list the reader is looking at, and the two things they can
/// do to it. Shares its state with the explorer's save button, and owns its own paging.
/// </summary>
public class VariableListViewTest : BunitContext
{
    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");

    private static VariableListItem Item(string name, string code) =>
        new()
        {
            VariableId = Guid.NewGuid(),
            AddedAt = DateTimeOffset.UtcNow,
            VariableName = name,
            VariableCode = code,
            KildeName = "Als registeret",
            KildeShortName = "ALS",
            DatasamlingName = "Inklusjon",
            VariabelgruppeName = "Ikke oppgitt",
            DataType = "2"
        };

    /// <summary>An entry whose variable has no row in the read model: id and timestamp, nothing else.</summary>
    private static VariableListItem Orphan() =>
        new() { VariableId = Guid.NewGuid(), AddedAt = DateTimeOffset.UtcNow };

    private sealed class ListClient(params VariableListItem[] items) : EmptyMuninExplorerClient
    {
        public int VariablesCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public int LastPageAsked { get; private set; }
        public int PageSize { get; init; } = 25;
        public bool HasList { get; init; } = true;

        /// <summary>Datatype names as the filters endpoint answers them, or none at all.</summary>
        public IReadOnlyList<DataTypeFacet> DataTypeFacets { get; init; } = [];

        public int FilterCalls { get; private set; }

        public string? LastFilterSearch { get; private set; }

        public VariableFilter? LastFilterFilter { get; private set; }

        private readonly List<VariableListItem> _items = [.. items];

        public static readonly Guid SecondListId = new("22222222-2222-2222-2222-222222222222");

        /// <summary>How many lists the reader has. Two of them makes the picker appear.</summary>
        public int ListCount { get; init; } = 1;

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            if (ListsThrow)
            {
                throw new InvalidOperationException("too many requests");
            }

            if (!HasList)
            {
                return Task.FromResult<IReadOnlyList<VariableList>>([]);
            }

            List<VariableList> lists = [new VariableList { Id = ListId, Name = "Mine hjertevariabler" }];

            if (ListCount > 1)
            {
                lists.Add(new VariableList { Id = SecondListId, Name = "Hjerte og kar" });
            }

            return Task.FromResult<IReadOnlyList<VariableList>>(lists);
        }

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new VariableList { Id = Guid.NewGuid(), Name = name });
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            VariablesCalls++;
            LastPageAsked = page;

            // Honours the size the component asked for, not the fake's own: slicing by an
            // internal number would hide a component that sent an unexpected page size.
            var slice = _items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = slice,
                TotalCount = _items.Count,
                PageNumber = page,
                Size = pageSize,
                // Computed, not hardcoded: a fake that always said one page let a component
                // ignoring the field pass its own paging test.
                TotalPages = Math.Max(1, (int)Math.Ceiling(_items.Count / (double)pageSize))
            });
        }

        /// <summary>The ids the export was asked for, so a test can see what would be in the file.</summary>
        public IReadOnlyCollection<Guid>? ExportedIds { get; private set; }

        /// <summary>Set when the reader's lists cannot be read - a throttled call, for instance.</summary>
        public bool ListsThrow { get; init; }

        /// <summary>Set when the test wants the export to fail the way a blocked browser would.</summary>
        public bool ExportThrows { get; init; }

        public override Task<ExportedList> ExportListAsync(
            IReadOnlyCollection<Guid> variableIds,
            ExportFormat format = ExportFormat.Xlsx,
            bool includeKodeverk = false,
            Guid? kildeIdFilter = null,
            CancellationToken cancellationToken = default)
        {
            ExportedIds = variableIds;

            return ExportThrows
                ? throw new InvalidOperationException("the browser refused")
                : Task.FromResult(new ExportedList([1, 2, 3], "text/csv", "variabelliste.csv"));
        }

        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null,
            VariableFilter? filter = null,
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            FilterCalls++;
            LastFilterSearch = search;
            LastFilterFilter = filter;
            return Task.FromResult(new FilterOptions { DataTypes = DataTypeFacets });
        }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            _items.RemoveAll(i => variableIds.Contains(i.VariableId));
            return Task.FromResult(true);
        }
    }

    private IRenderedComponent<VariableListView> RenderView(ListClient client, bool signedIn = true)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        return Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, signedIn));
    }

    // -----------------------------------------------------------------------

    [Fact]
    public void View_WhenTheReaderIsSignedOut_ThenNothingIsDrawnAndNothingIsAsked()
    {
        // Asserted on the call count, not on the markup: an implementation that fetches and swallows
        // the 401 renders the same nothing while sending a failed request per render.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));

        var cut = RenderView(client, signedIn: false);

        Assert.Empty(cut.Markup.Trim());
        Assert.Equal(0, client.VariablesCalls);
    }

    [Fact]
    public void View_WhenTheListHasVariables_ThenTheyAreShownWithTheirColumns()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));

        var cut = RenderView(client);

        Assert.Contains("Alder ved diagnose", cut.Markup);
        Assert.Contains("V_BDR.ALDER", cut.Markup);
        Assert.Contains("ALS", cut.Markup);
        Assert.Contains("Inklusjon", cut.Markup);
    }

    [Fact]
    public void View_WhenTheApiNamesADatatype_ThenTheNameIsShownRatherThanTheCode()
    {
        // The list stores the code the search endpoint hands out. Left alone it renders as "2" beside
        // an explorer showing "Heltall" for the same variable, on the same page.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            DataTypeFacets = [new DataTypeFacet { Value = "2", DisplayName = "Heltall" }]
        };

        var cut = RenderView(client);

        Assert.Contains("Heltall", cut.Markup);
    }

    [Fact]
    public void View_WhenTheNamesAreAskedFor_ThenItIsWithoutASearchOrAFilter()
    {
        // Scoped to a search, the answer only names the datatypes that search happens to match - and
        // this view names codes the reader saved under some other search entirely.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            DataTypeFacets = [new DataTypeFacet { Value = "2", DisplayName = "Heltall" }]
        };

        RenderView(client);

        Assert.Equal(1, client.FilterCalls);
        Assert.Null(client.LastFilterSearch);
        Assert.Null(client.LastFilterFilter);
    }

    [Fact]
    public void View_WhenTheApiHasNoNameForTheCode_ThenTheCodeIsShownRatherThanNothing()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));

        var cut = RenderView(client);

        Assert.Contains(">2<", cut.Markup);
    }

    [Fact]
    public void View_WhenTheListIsEmpty_ThenItSaysSoRatherThanShowingAnEmptyTable()
    {
        var cut = RenderView(new ListClient());

        Assert.Contains("tom", cut.Markup);
    }

    [Fact]
    public void View_WhenTheReaderHasNoLists_ThenItSaysSo()
    {
        var cut = RenderView(new ListClient { HasList = false });

        Assert.Contains("ingen variabellister", cut.Markup);
    }

    // -----------------------------------------------------------------------
    // The traps.
    // -----------------------------------------------------------------------

    [Fact]
    public void View_WhenAnEntryHasNoRowInTheReadModel_ThenItKeepsItsPlace()
    {
        // The API returns it deliberately so the paging totals stay honest. A view that filtered
        // rows without a name would show 1 of 2 under a count that says 2, and the reader would
        // never learn something had gone.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"), Orphan());

        var cut = RenderView(client);

        Assert.Equal(2, cut.FindAll(".munin-explorer-data-list__item").Count);
        Assert.Contains("ikke tilgjengelig", cut.Markup);
    }

    [Fact]
    public async Task View_WhenTheListIsLongerThanAPage_ThenTheReaderCanReachTheSecondPage()
    {
        // A view that fetched page one and called it the list would show the first few and hide the
        // rest without saying so.
        var many = Enumerable.Range(1, 30).Select(i => Item($"Variabel {i}", $"V_{i}")).ToArray();
        var client = new ListClient(many) { PageSize = 25 };
        var cut = RenderView(client);

        Assert.Contains("Variabel 1", cut.Markup);
        Assert.DoesNotContain("Variabel 30", cut.Markup);

        await cut.InvokeAsync(() => cut.FindAll(".munin-explorer-pagination-content button")[^1].Click());

        Assert.Equal(2, client.LastPageAsked);
        Assert.Contains("Variabel 30", cut.Markup);
    }

    [Fact]
    public void View_WhenAVariableIsRemoved_ThenItLeavesTheListAndTheApiWasAsked()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));
        var cut = RenderView(client);

        cut.FindAll(".munin-explorer-dataitem-main button")[0].Click();

        Assert.Equal(1, client.RemoveCalls);
        Assert.DoesNotContain("Alder ved diagnose", cut.Markup);
    }

    [Fact]
    public void View_WhenANameIsEnteredAndCreatePressed_ThenTheListIsMade()
    {
        var client = new ListClient { HasList = false };
        var cut = RenderView(client);

        // Change, not Input: the field binds on onchange rather than oninput, because one
        // round trip per keystroke drops characters on a paste inside a Blazor Server circuit.
        cut.Find("input[type=text]").Change("Hjerte og kar");
        cut.Find(".munin-explorer-container button").Click();

        Assert.Equal(1, client.CreateCalls);
    }

    [Fact]
    public async Task View_WhenAnotherSurfaceRemovesAVariable_ThenItLeavesThisViewToo()
    {
        // The rows come from this component's own page, which the holder does not own. Re-rendering
        // on Changed is not enough - the page has to be read again, or the removed row stays.
        var item = Item("Alder ved diagnose", "V_BDR.ALDER");
        var client = new ListClient(item);
        var cut = RenderView(client);
        Assert.Contains("Alder ved diagnose", cut.Markup);

        // The save button's path, not this view's: straight at the shared holder.
        var state = Services.GetRequiredService<VariableListState>();
        await cut.InvokeAsync(() => state.RemoveVariablesAsync(ListId, [item.VariableId]));

        Assert.DoesNotContain("Alder ved diagnose", cut.Markup);
    }

    [Fact]
    public async Task View_WhenTheLastRowOfAPageIsRemoved_ThenTheReaderIsNotStrandedOnAnEmptyPage()
    {
        // Page two of two, one row on it. Removing that row leaves a page that no longer exists,
        // and the empty state replaces the pager - so without a retreat the reader is told the list
        // is empty with no control left to reach the rows still on page one.
        var many = Enumerable.Range(1, 26).Select(i => Item($"Variabel {i}", $"V_{i}")).ToArray();
        var client = new ListClient(many) { PageSize = 25 };
        var cut = RenderView(client);

        await cut.InvokeAsync(() => cut.FindAll(".munin-explorer-pagination-content button")[^1].Click());
        Assert.Contains("Variabel 26", cut.Markup);

        cut.FindAll(".munin-explorer-dataitem-main button")[0].Click();

        Assert.Contains("Variabel 1", cut.Markup);
        Assert.DoesNotContain("Denne listen er tom", cut.Markup);
    }

    [Fact]
    public async Task View_WhenTheReaderHasTwoLists_ThenSwitchingShowsTheOtherOne()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2 };
        var cut = RenderView(client);

        var before = client.VariablesCalls;
        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));

        Assert.True(client.VariablesCalls > before, "switching list did not read the other list");
    }

    [Fact]
    public async Task View_WhenTheListIsDownloaded_ThenEveryPageOfIdsIsSentNotJustTheOneOnScreen()
    {
        // The reader asked for their list. A file holding only the 25 rows they happened to be
        // looking at would be wrong in a way nobody notices until they open it.
        var many = Enumerable.Range(1, 30).Select(i => Item($"Variabel {i}", $"V_{i}")).ToArray();
        var client = new ListClient(many) { PageSize = 25 };
        var cut = RenderView(client);

        await cut.InvokeAsync(() => cut.FindAll("button")
            .First(b => b.TextContent.Contains("Excel", StringComparison.Ordinal)).Click());

        Assert.NotNull(client.ExportedIds);
        Assert.Equal(30, client.ExportedIds!.Count);
    }

    [Fact]
    public async Task View_WhenTheDownloadIsRefused_ThenTheReaderIsToldRatherThanLeftWithADeadButton()
    {
        // A Content-Security-Policy without blob: lands here. Silence would leave a button that
        // looks like it works and does nothing.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ExportThrows = true };
        var cut = RenderView(client);

        await cut.InvokeAsync(() => cut.FindAll("button")
            .First(b => b.TextContent.Contains("Excel", StringComparison.Ordinal)).Click());

        Assert.Contains("Kunne ikke laste ned", cut.Markup);
    }

    [Fact]
    public void View_WhenTheListsCannotBeRead_ThenItSaysSoRatherThanTakingTheCircuitDown()
    {
        // The read happens in a lifecycle method, and an exception out of one of those takes the
        // whole circuit with it - in helsedata's legacy host, the entire CMS page. The mount fires
        // this alongside the search and the facet refresh, which is the burst the rate limiter
        // counts, so a refusal here is ordinary rather than rare.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListsThrow = true };

        var cut = RenderView(client);

        Assert.Contains("Kunne ikke hente listen", cut.Markup);
    }

    [Fact]
    public void View_WhenNothingHasFailed_ThenTheAlertContainerIsAlreadyInTheDom()
    {
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var alert = cut.FindAll("[role=alert]");
        Assert.Single(alert);
        Assert.Equal("", alert[0].TextContent.Trim());
    }

    [Fact]
    public void View_WhenItIsDrawn_ThenEveryClassNameHasARuleInTheHostStylesheet()
    {
        // The package ships no CSS: a name with no rule behind it renders unstyled in the host.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"), Orphan()));

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }
}
