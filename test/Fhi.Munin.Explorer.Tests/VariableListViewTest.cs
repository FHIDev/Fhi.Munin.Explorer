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
        public int ListsCalls { get; private set; }
        public int RenameCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public string? LastRenamedTo { get; private set; }
        public int LastPageAsked { get; private set; }

        /// <summary>
        /// Every list whose variables were asked for. Which list, not how many times: a view asking
        /// for a deleted list makes exactly as many calls as one asking for a live list.
        /// </summary>
        public IReadOnlyList<Guid> VariablesAskedFor => _askedFor;

        private readonly List<Guid> _askedFor = [];
        private readonly HashSet<Guid> _deleted = [];
        public int PageSize { get; init; } = 25;
        public bool HasList { get; init; } = true;

        /// <summary>Datatype names as the filters endpoint answers them, or none at all.</summary>
        public IReadOnlyList<DataTypeFacet> DataTypeFacets { get; init; } = [];

        public int FilterCalls { get; private set; }

        public string? LastFilterSearch { get; private set; }

        public VariableFilter? LastFilterFilter { get; private set; }

        private readonly List<VariableListItem> _items = [.. items];

        public static readonly Guid SecondListId = new("22222222-2222-2222-2222-222222222222");
        public static readonly Guid ThirdListId = new("33333333-3333-3333-3333-333333333333");

        /// <summary>How many lists the reader has. Two of them makes the picker appear.</summary>
        public int ListCount { get; init; } = 1;

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            ListsCalls++;

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

            if (ListCount > 2)
            {
                lists.Add(new VariableList { Id = ThirdListId, Name = "Kreft og svulster" });
            }

            return Task.FromResult<IReadOnlyList<VariableList>>([.. lists.Where(l => !_deleted.Contains(l.Id))]);
        }

        /// <summary>How a rename should fail, or null for one the API accepts.</summary>
        public Exception? RenameThrows { get; init; }

        /// <summary>How a delete should fail, or null for one the API accepts.</summary>
        public Exception? DeleteThrows { get; init; }

        /// <summary>Run while the rename is still in flight, so a test can raise another change.</summary>
        public Func<Task>? DuringRename { get; init; }

        public override async Task<bool> RenameMyListAsync(Guid id, string name, CancellationToken cancellationToken = default)
        {
            RenameCalls++;

            if (RenameThrows is not null)
            {
                throw RenameThrows;
            }

            if (DuringRename is not null)
            {
                await DuringRename();
            }

            LastRenamedTo = name;
            return true;
        }

        public override Task<bool> DeleteMyListAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;

            if (DeleteThrows is not null)
            {
                throw DeleteThrows;
            }

            _deleted.Add(id);
            return Task.FromResult(true);
        }

        /// <summary>Set when the test wants creating a list to fail the way a lost API would.</summary>
        /// <remarks>Settable, unlike the export switches: a test turns it off to try again after.</remarks>
        public bool CreateThrows { get; set; }

        /// <summary>Set when the test wants the create refused with the API's 429.</summary>
        public bool CreateThrottles { get; set; }

        /// <summary>Set when the switch to the list just created is what fails.</summary>
        /// <remarks>
        /// Aimed at that one list, so the failure lands on the switch and not on the reads
        /// around it - the point is that the list was made and only the switch was lost.
        /// </remarks>
        public bool ActivateThrows { get; set; }

        /// <summary>Set when the switch to the list just created is refused with the API's 429.</summary>
        /// <remarks>Its own switch beside <see cref="ActivateThrows"/>, for the reason the export pair has one.</remarks>
        public bool ActivateThrottles { get; set; }

        private VariableList? _created;

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            CreateCalls++;

            if (CreateThrottles)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            if (CreateThrows)
            {
                throw new InvalidOperationException("the API is gone");
            }

            _created = new VariableList { Id = Guid.NewGuid(), Name = name };
            return Task.FromResult(_created);
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            VariablesCalls++;
            LastPageAsked = page;
            _askedFor.Add(id);

            // The API's own answer for a list the caller no longer has: not an error, and not an
            // empty list either. A view that reads it as "empty" draws a table for a list that is
            // gone, which is the failure Fhi.Metadata-fjiba is about.
            if (_deleted.Contains(id))
            {
                return Task.FromResult<Page<VariableListItem>?>(null);
            }

            if (_created is not null && id == _created.Id)
            {
                if (ActivateThrottles)
                {
                    throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
                }

                if (ActivateThrows)
                {
                    throw new InvalidOperationException("the membership read is gone");
                }
            }

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

        /// <summary>Set when the test wants the export refused with the API's 429.</summary>
        /// <remarks>
        /// Its own switch beside <see cref="ExportThrows"/> so the pair can be asserted against
        /// each other: the alert has to say something different for each, and one flag could not
        /// show that.
        /// </remarks>
        public bool ExportThrottles { get; init; }

        public override Task<ExportedList> ExportListAsync(
            IReadOnlyCollection<Guid> variableIds,
            ExportFormat format = ExportFormat.Xlsx,
            bool includeKodeverk = false,
            Guid? kildeIdFilter = null,
            CancellationToken cancellationToken = default)
        {
            ExportedIds = variableIds;

            if (ExportThrottles)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

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

        /// <summary>How a removal should fail, or null for one the API accepts.</summary>
        /// <remarks>Settable, not init: a test turns it off to show the view still answers after.</remarks>
        public Exception? RemoveThrows { get; set; }

        /// <summary>Set when the API declines the removal: it returns false rather than throwing.</summary>
        /// <remarks>Settable for the reason <see cref="RemoveThrows"/> is.</remarks>
        public bool RemoveIsDeclined { get; set; }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;

            if (RemoveThrows is not null)
            {
                throw RemoveThrows;
            }

            if (RemoveIsDeclined)
            {
                return Task.FromResult(false);
            }

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
    public void View_WhenTheReaderIsSignedOut_ThenTheNamesAreNotAskedForEither()
    {
        // Same reason the list itself is not read: the view draws nothing for a signed-out reader, and
        // a call whose answer nobody sees still counts against the limiter.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            DataTypeFacets = [new DataTypeFacet { Value = "2", DisplayName = "Heltall" }]
        };

        RenderView(client, signedIn: false);

        Assert.Equal(0, client.FilterCalls);
    }

    [Fact]
    public void View_WhenTheLanguageChanges_ThenTheNamesAreReadAgainInTheNewOne()
    {
        // The names come back in the language they were asked for. Cached on "have they been read"
        // alone, a host switching language would keep showing the previous one until the component
        // was recreated.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            DataTypeFacets = [new DataTypeFacet { Value = "2", DisplayName = "Heltall" }]
        };

        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, "no"));

        Assert.Equal(1, client.FilterCalls);

        cut.Render(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, "en"));

        Assert.Equal(2, client.FilterCalls);
    }

    [Fact]
    public void View_WhenTheFallbackIsShown_ThenItIsNotMarkedAsNorwegian()
    {
        // The cells carry lang="no" because catalogue text is Norwegian whatever language the page is
        // in. The fallback is ours, in the reader's language, so the same marking would have a screen
        // reader pronounce an English phrase as Norwegian.
        var cut = RenderView(new ListClient(Orphan()));

        Assert.DoesNotContain("lang=\"no\">Ikke oppgitt", cut.Markup);
    }

    [Fact]
    public void View_WhenAPeriodIsOpenEnded_ThenItReadsTheWayTheExplorerWritesIt()
    {
        // "2021-" was this view's own shorthand. The explorer, one region up the same page, writes
        // the month and the catalogue's word for a period that has not ended.
        var item = Item("Alder ved diagnose", "V_BDR.ALDER") with
        {
            DataFrom = new DateTimeOffset(2021, 8, 1, 0, 0, 0, TimeSpan.Zero),
            DataTo = null
        };

        var cut = RenderView(new ListClient(item));

        Assert.Contains("2021", cut.Markup);
        Assert.Contains("Pågående", cut.Markup);
        Assert.DoesNotContain("2021–<", cut.Markup);
    }

    [Fact]
    public void View_WhenAFieldWasNeverSet_ThenItSaysSoRatherThanLeavingTheCellBlank()
    {
        // An empty cell beside a filled one reads as data that went missing, not data nobody entered.
        var cut = RenderView(new ListClient(Orphan()));

        Assert.Contains("Ikke oppgitt", cut.Markup);
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
    public void View_WhenTheListHasVariables_ThenTheRowsExposeTableStructure()
    {
        // Fhi.Metadata-3b1l4. These rows share `munin-explorer-data-list` with the explorer's own
        // result list, so they shared its failure: no row, column or header of the reader's own
        // saved list reached assistive technology. axe never reported it — there is no sortable
        // header here to hang an invalid aria-sort on, and missing structure is not a rule
        // violation, it is just missing.
        //
        // As in the explorer's own cases, this asserts the attributes and the shape. That a browser
        // resolves them under the stylesheet was checked by hand in Chrome's accessibility tree,
        // with the sample stylesheet and with Stiler's own compiled main.css.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var table = cut.Find(".munin-explorer-data-list[role='table']");

        // No rowgroups: the header row and the data rows are siblings here, unlike the explorer's
        // list, and a table may own rows directly.
        Assert.Equal("row", table.QuerySelector(".munin-explorer-dataitem-header")!.GetAttribute("role"));
        Assert.Equal(7, cut.FindAll("[role='columnheader']").Count);

        var row = cut.Find(".munin-explorer-data-list__item");

        Assert.Equal("row", row.GetAttribute("role"));
        Assert.Equal("rowheader",
                     row.QuerySelector(".munin-explorer-dataitem-main__name")!.GetAttribute("role"));

        // Six value columns and the cell holding the remove button, which is there because a row
        // owns nothing but cells and a <button> cannot be one without ceasing to be a button.
        var cells = row.QuerySelectorAll("[role='cell']");

        Assert.Equal(7, cells.Length);
        Assert.Equal("BUTTON", cells[^1].Children[0].TagName);

        // The boxes that only lay the columns out step out of the tree, or they sit between the
        // row and the cells it owns.
        Assert.Equal("none", row.QuerySelector(".munin-explorer-data-list__item__row")!.GetAttribute("role"));
        Assert.Equal("none", row.QuerySelector(".munin-explorer-dataitem-main")!.GetAttribute("role"));
    }

    /// <summary>The text of one named column's cell in the first row of the list.</summary>
    private static string CellText(IRenderedComponent<VariableListView> cut, string key) =>
        cut.Find($".munin-explorer-dataitem-main__{key} .munin-explorer-dataitem-main__column__text")
           .TextContent;

    [Fact]
    public void View_WhenACellIsDrawn_ThenItIsTheCellTheResultListDraws()
    {
        // Both surfaces draw these columns, and now from one helper. Written out twice they looked
        // alike and could stop being alike without anything failing: the hidden field name and the
        // full kilde name on hover were in the explorer's cells and in none of these.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var cell = cut.Find(".munin-explorer-dataitem-main__source");

        Assert.Equal("cell", cell.GetAttribute("role"));
        Assert.Equal("Kilde: ", cell.QuerySelector(".screenreader-only")!.TextContent);
        Assert.Equal("ALS", cell.QuerySelector(".munin-explorer-dataitem-main__column__text")!.TextContent);
        Assert.Equal("Als registeret", cell.GetAttribute("title"));
    }

    [Fact]
    public async Task View_WhenThePageChanges_ThenEveryCellStillHoldsItsOwnValue()
    {
        // The cells are built with explicit sequence numbers, which Blazor uses positionally to
        // diff one render against the next. A number that repeats or goes backwards patches one
        // column's node with another's — and only from the second render, never the first.
        var many = Enumerable.Range(1, 30)
            .Select(i => Item($"Variabel {i}", $"V_{i}") with { DatasamlingName = $"Samling {i}", VariabelgruppeName = $"Gruppe {i}" })
            .ToArray();

        var cut = RenderView(new ListClient(many) { PageSize = 25 });

        await cut.InvokeAsync(() => cut.FindAll(".munin-explorer-pagination-content button")[^1].Click());

        Assert.Equal("V_26", CellText(cut, "code"));
        Assert.Equal("ALS", CellText(cut, "source"));
        Assert.Equal("Samling 26", CellText(cut, "dataCollection"));
        Assert.Equal("Gruppe 26", CellText(cut, "theme"));

        // The hidden field name travels with the value, not with the position it was drawn in.
        Assert.Equal("Datasamling: ",
                     cut.Find(".munin-explorer-dataitem-main__dataCollection .screenreader-only").TextContent);
    }

    [Fact]
    public void View_WhenAListIsShown_ThenTheTableIsNamedAfterIt()
    {
        // The reader's own word for the list, not the view's heading: with several lists saved,
        // "Mine variabellister" would name every table the same and a reader moving between them
        // could not tell which one is on screen.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        Assert.Equal("Mine hjertevariabler", cut.Find("[role='table']").GetAttribute("aria-label"));
    }

    [Fact]
    public void View_WhenAnEntryHasNoRowInTheReadModel_ThenItKeepsItsPlace()
    {
        // The API returns it deliberately so the paging totals stay honest. A view that filtered
        // rows without a name would show 1 of 2 under a count that says 2, and the reader would
        // never learn something had gone.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"), Orphan());

        var cut = RenderView(client);

        Assert.Equal(2, cut.FindAll(".munin-explorer-data-list__item").Count);

        // Scoped to the cell, not to the document. The remove button on that same row is named
        // from these words, so a substring assertion over cut.Markup is satisfied by the button
        // alone — and RowName could return "" for an orphan, leaving a blank name cell under a
        // count that says two, with every case in this file still green.
        var names = cut.FindAll(".munin-explorer-dataitem-main__name");

        Assert.Equal(2, names.Count);
        Assert.Equal("Alder ved diagnose", names[0].TextContent.Trim());
        Assert.Equal("Variabelen er ikke tilgjengelig lenger", names[1].TextContent.Trim());
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

        // The converse of the declined removal below. Without it, a handler that reported every
        // removal as failed would still pass this test on the row alone.
        Assert.DoesNotContain("Kunne ikke endre listen", cut.Markup);
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
    public void View_WhenCreatingAListIsRateLimited_ThenItSaysSoRatherThanThatTheSaveFailed()
    {
        var client = new ListClient { HasList = false, CreateThrottles = true };
        var cut = RenderView(client);

        cut.Find("input[type=text]").Change("Hjerte og kar");
        cut.Find(".munin-explorer-container button").Click();

        Assert.Contains("for mange forespørsler", cut.Markup);
        Assert.DoesNotContain("Kunne ikke lagre", cut.Markup);
    }

    [Fact]
    public void View_WhenCreatingAListThrows_ThenTheAlertSaysTheSaveFailed()
    {
        var client = new ListClient { HasList = false, CreateThrows = true };
        var cut = RenderView(client);

        cut.Find("input[type=text]").Change("Hjerte og kar");
        cut.Find(".munin-explorer-container button").Click();

        Assert.Contains("Kunne ikke lagre", cut.Markup);
        Assert.DoesNotContain("for mange forespørsler", cut.Markup);
    }

    [Fact]
    public void View_WhenCreatingAListFails_ThenTheViewIsStillThereToTryAgain()
    {
        // The message is the smaller half. Unguarded, the throw leaves the event handler and the
        // circuit goes with it - so what has to be shown is that the view still answers after.
        var client = new ListClient { HasList = false, CreateThrows = true };
        var cut = RenderView(client);

        cut.Find("input[type=text]").Change("Hjerte og kar");
        cut.Find(".munin-explorer-container button").Click();

        client.CreateThrows = false;
        cut.Find("input[type=text]").Change("Hjerte og kar");
        cut.Find(".munin-explorer-container button").Click();

        Assert.Equal(2, client.CreateCalls);
        Assert.DoesNotContain("Kunne ikke lagre", cut.Markup);
    }

    [Fact]
    public void View_WhenTheNewListCannotBeSwitchedTo_ThenItSaysSoRatherThanThrowing()
    {
        // The list was made and is on the server; it is the switch to it that was lost, which is
        // what ListLoadError says. ChooseListAsync has guarded its own switch all along.
        var client = new ListClient { HasList = false, ActivateThrows = true };
        var cut = RenderView(client);

        cut.Find("input[type=text]").Change("Hjerte og kar");
        cut.Find(".munin-explorer-container button").Click();

        Assert.Equal(1, client.CreateCalls);
        Assert.Contains("Kunne ikke hente listen", cut.Markup);
        Assert.NotNull(cut.Find("input[type=text]"));
    }

    [Fact]
    public void View_WhenTheSwitchToTheNewListIsRateLimited_ThenItSaysSoAndTheViewIsStillWorking()
    {
        // The other half of the pair above: refused for too many requests, not lost, so the
        // reader is asked to wait. The words alone would pass for a handler that threw and took
        // the circuit with it, so the second create is what says it returned.
        var client = new ListClient { HasList = false, ActivateThrottles = true };
        var cut = RenderView(client);

        cut.Find("input[type=text]").Change("Hjerte og kar");
        cut.Find(".munin-explorer-container button").Click();

        Assert.Contains("for mange forespørsler", cut.Markup);
        Assert.DoesNotContain("Kunne ikke hente listen", cut.Markup);

        client.ActivateThrottles = false;
        cut.Find("input[type=text]").Change("Hjerte og kar");
        cut.Find(".munin-explorer-container button").Click();

        Assert.Equal(2, client.CreateCalls);
        Assert.DoesNotContain("for mange forespørsler", cut.Markup);
    }

    [Fact]
    public void View_WhenACreateFailsAfterALoadFailed_ThenTheAlertAnswersForTheCreate()
    {
        // Both sentences live in one alert region. Left standing, the older one answers for an
        // action the reader did not just take - here, "could not fetch" for a failed save.
        var client = new ListClient { HasList = false, ListsThrow = true, CreateThrows = true };
        var cut = RenderView(client);
        Assert.Contains("Kunne ikke hente listen", cut.Markup);

        cut.Find("input[type=text]").Change("Hjerte og kar");
        cut.Find(".munin-explorer-container button").Click();

        Assert.Contains("Kunne ikke lagre", cut.Markup);
        Assert.DoesNotContain("Kunne ikke hente listen", cut.Markup);
    }

    [Fact]
    public async Task View_WhenACreateFailsAfterARenameFailed_ThenTheAlertAnswersForTheCreate()
    {
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            RenameThrows = new InvalidOperationException("the API is gone"),
            CreateThrows = true
        };
        var cut = RenderView(client);

        cut.FindAll("input[type=text]")[1].Change("Hjertet mitt");
        await PressAsync(cut, "Gi nytt navn");
        Assert.Contains("Kunne ikke endre listen", cut.Markup);

        cut.FindAll("input[type=text]")[0].Change("Hjerte og kar");
        await PressAsync(cut, "Opprett liste");

        Assert.Contains("Kunne ikke lagre", cut.Markup);
        Assert.DoesNotContain("Kunne ikke endre listen", cut.Markup);
    }

    [Fact]
    public async Task View_WhenTheReaderSwitchesListAfterAFailedCreate_ThenTheSentenceGoes()
    {
        // Switching list is an action too. The alert would otherwise still be answering for a
        // save the reader has since moved on from.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            ListCount = 2,
            CreateThrows = true
        };
        var cut = RenderView(client);

        cut.FindAll("input[type=text]")[0].Change("Hjerte og kar");
        await PressAsync(cut, "Opprett liste");
        Assert.Contains("Kunne ikke lagre", cut.Markup);

        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));

        Assert.DoesNotContain("Kunne ikke lagre", cut.Markup);
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
        Assert.DoesNotContain("for mange forespørsler", cut.Markup);
    }

    [Fact]
    public async Task View_WhenTheDownloadIsRateLimited_ThenItSaysSoRatherThanThatTheDownloadFailed()
    {
        // The export and the id walk in front of it both count against the browse policy, so a
        // long list is a plausible way to meet it. The generic sentence leaves the reader guessing
        // at a cause the component already knows.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ExportThrottles = true };
        var cut = RenderView(client);

        await cut.InvokeAsync(() => cut.FindAll("button")
            .First(b => b.TextContent.Contains("Excel", StringComparison.Ordinal)).Click());

        Assert.Contains("for mange forespørsler", cut.Markup);
        Assert.DoesNotContain("Kunne ikke laste ned", cut.Markup);
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
    public void View_WhenTheCreateFormIsDrawn_ThenTheNameFieldIsNamedBySomethingThatIsNotAPlaceholder()
    {
        // The field shipped as a bare input carrying a placeholder and nothing else, so a screen
        // reader announced an unnamed edit field. Asserted on the accessible NAME rather than on
        // the presence of a naming attribute: a placeholder, or a title, satisfies "has an
        // attribute" while leaving the control unnamed, and several checking tools accept either.
        // AccessibleName resolves only the sources that really are names, which is the point of it.
        var cut = RenderView(new ListClient { HasList = false });

        var field = cut.Find("input[type=text]");

        Assert.Equal("Navn på ny liste", AccessibleName.Of(field));

        // And the name is not a tooltip wearing a disguise. `title` is the other attribute a naive
        // check counts, and mobile screen readers ignore it.
        Assert.Null(field.GetAttribute("title"));

        // The half a placeholder cannot do: the name has to survive the reader typing into the
        // field, which is the moment a placeholder disappears.
        field.Change("Hjerte og kar");
        Assert.Equal("Navn på ny liste", AccessibleName.Of(cut.Find("input[type=text]")));
    }

    [Fact]
    public void View_WhenTheListHasSeveralVariables_ThenEachRemoveButtonNamesItsOwnVariable()
    {
        // Two rows, not one: a constant aria-label passes "the button has an accessible name" and
        // still leaves a screen reader user hearing "Fjern, Fjern, Fjern" with no way to tell which
        // variable each one takes out. The distinctness assertion is what catches that.
        var cut = RenderView(new ListClient(
            Item("Alder ved diagnose", "V_BDR.ALDER"),
            Item("Skjemastatus", "V_BDR.FORMSTATUS")));

        var names = cut.FindAll(".munin-explorer-dataitem-main button")
            .Select(AccessibleName.Of)
            .ToList();

        Assert.Equal(2, names.Count);
        Assert.Contains("Alder ved diagnose", names[0], StringComparison.Ordinal);
        Assert.Contains("Skjemastatus", names[1], StringComparison.Ordinal);
        Assert.Equal(2, names.Distinct(StringComparer.Ordinal).Count());

        // The word on the button stays in the sentence, so a speech-input user saying what they
        // can see still hits the control. WCAG 2.5.3.
        Assert.All(names, name => Assert.Contains("Fjern", name, StringComparison.Ordinal));
    }

    [Fact]
    public void View_WhenThePageIsEnglish_ThenTheRemoveButtonKeepsEachHalfOfItsNameInItsOwnLanguage()
    {
        // The reason the name is two elements rather than one aria-label. "Remove" is ours and
        // follows Language; "Alder ved diagnose" is Munin's and is Norwegian whatever the
        // surrounding UI is. Written as one string the whole sentence would reach an English
        // voice, which pronounces the Norwegian half with English phonetics — WCAG 3.1.2, and the
        // defect the lang="no" on these cells exists to prevent.
        Services.AddSingleton<IMuninExplorerClient>(
            new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")));
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, "en"));

        var button = cut.Find(".munin-explorer-dataitem-main button");

        Assert.Equal("Remove Alder ved diagnose", AccessibleName.Of(button));

        // Not an aria-label: a single string is exactly what cannot carry the two languages.
        Assert.Null(button.GetAttribute("aria-label"));

        // The half that is Munin's is marked as Norwegian where the button borrows it from.
        var referenced = button.GetAttribute("aria-labelledby")!.Split(' ');
        var nameCell = cut.Find($"#{referenced[1]}");

        Assert.Equal("Alder ved diagnose", nameCell.TextContent.Trim());
        Assert.Equal("no", nameCell.GetAttribute("lang"));

        // And the button's own word comes first, so speech input reaches it by what is on screen.
        Assert.Equal(button.Id, referenced[0]);
    }

    [Fact]
    public void View_WhenAnEntryHasNoVariableLeft_ThenItsRemoveButtonSaysSoWithoutReadingAnIdOut()
    {
        // An orphaned entry has no name to borrow, so the button borrows the sentence its name
        // cell shows instead — it still says what it removes, and it says it in words.
        //
        // Named from the cell rather than from the entry's id, which was the first attempt at
        // telling two orphans apart: NVDA and JAWS spell a hex GUID out character by character,
        // so that is about ten seconds of speech per row, naming the row after a value the row
        // does not show in any column a reader could match it against.
        var orphan = Orphan();
        var cut = RenderView(new ListClient(orphan));

        var name = AccessibleName.Of(cut.Find(".munin-explorer-dataitem-main button"));

        Assert.Contains("ikke tilgjengelig lenger", name, StringComparison.Ordinal);
        Assert.Contains("Fjern", name, StringComparison.Ordinal);
        Assert.DoesNotContain(orphan.VariableId.ToString(), name, StringComparison.Ordinal);
        Assert.DoesNotContain(
            orphan.VariableId.ToString("N"), name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void View_WhenTwoEntriesHaveNoVariableLeft_ThenTheirButtonsShareANameAndNotAnId()
    {
        // Two orphans do announce alike, deliberately. A duplicate accessible name on two distinct
        // controls is not a WCAG failure — 4.1.2 asks for a name and 2.4.6 asks that it describe,
        // neither that it be unique — and the only thing that could tell them apart is an id no
        // column shows. What must NOT collide is the DOM ids the names are built from: two rows
        // sharing one would aim both buttons at the first row's cell, which is a 4.1.1 failure.
        var first = Orphan();
        var second = Orphan();
        var cut = RenderView(new ListClient(first, second));

        var buttons = cut.FindAll(".munin-explorer-dataitem-main button");

        Assert.Equal(2, buttons.Count);
        Assert.Equal(AccessibleName.Of(buttons[0]), AccessibleName.Of(buttons[1]));
        Assert.NotEqual(buttons[0].Id, buttons[1].Id);
        Assert.NotEqual(
            buttons[0].GetAttribute("aria-labelledby"), buttons[1].GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void View_WhenTheReaderHasTwoLists_ThenThePickerIsNamedByItsLabelAndNotByItsOptions()
    {
        // The picker is a <select> inside its own <label>, so its name comes from the words around
        // it — not from the options, which are the reader's list names and would make the control
        // announce as "Velg liste Mine hjertevariabler Hjerte og kar". The option text is the
        // select's value, not its name.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2 });

        Assert.Equal("Velg liste", AccessibleName.Of(cut.Find("select")));
    }

    [Fact]
    public void View_WhenTwoViewsAreOnOnePage_ThenTheirNameFieldsDoNotShareAnId()
    {
        // The host decides where this component goes, and helsedata's CMS can put two of it on one
        // page. Duplicate ids are a WCAG 4.1.1 failure, and here they cost the thing the label was
        // added for: both <label for> would resolve to whichever field rendered first, leaving the
        // second one unnamed again. Nothing catches that in a page with one mount — the shape this
        // borrows from the explorer's own guard, VariableExplorerTest.cs
        // Source_WhenTwoExplorersAreOnOnePage_ThenTheirPanelsDoNotShareIds.
        Services.AddSingleton<IMuninExplorerClient>(new ListClient { HasList = false });
        Services.AddScoped<VariableListState>();

        var a = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
        var b = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));

        var first = a.Find("input[type=text]");
        var second = b.Find("input[type=text]");

        Assert.NotEqual(first.Id, second.Id);

        // And each label points at its own field rather than at the same one, which is the half an
        // id comparison does not cover: two different ids with both labels aimed at the first
        // would still leave the second field unnamed.
        Assert.Equal(first.Id, a.Find("label[for]").GetAttribute("for"));
        Assert.Equal(second.Id, b.Find("label[for]").GetAttribute("for"));

        Assert.Equal("Navn på ny liste", AccessibleName.Of(first));
        Assert.Equal("Navn på ny liste", AccessibleName.Of(second));
    }

    /// <summary>Presses the button whose visible word is exactly this, the way a reader finds it.</summary>
    private static Task PressAsync(IRenderedComponent<VariableListView> cut, string word) =>
        cut.InvokeAsync(() => cut.FindAll("button")
            .First(b => b.TextContent.Trim() == word).Click());

    [Fact]
    public async Task View_WhenTheListIsRenamed_ThenTheNewNameShowsWithoutReadingAnythingAgain()
    {
        // Acceptance 1 of Fhi.Metadata-fjiba. The holder patches its own copy and tells the other
        // surfaces, so a case asserting only that the name changed would pass for a view that
        // refetched. The call counts are what say it did not.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2 };
        var cut = RenderView(client);

        var lists = client.ListsCalls;
        var variables = client.VariablesCalls;

        cut.FindAll("input[type=text]")[1].Change("Hjertet mitt");
        await PressAsync(cut, "Gi nytt navn");

        Assert.Equal(1, client.RenameCalls);
        Assert.Equal("Hjertet mitt", client.LastRenamedTo);
        Assert.Equal(lists, client.ListsCalls);
        Assert.Equal(variables, client.VariablesCalls);

        // On screen in the picker, which is where the reader's own word for a list is shown.
        Assert.Contains("Hjertet mitt", cut.Markup);
        Assert.DoesNotContain("Mine hjertevariabler", cut.Markup);
    }

    [Fact]
    public async Task View_WhenTheRenameIsThrottled_ThenItSaysSoAndTheViewIsStillWorking()
    {
        // A 429 here is ordinary: these writes meet the same limiter as every read on the page.
        // Asserting the words alone would pass for a handler that threw and took the circuit with
        // it, so switching list afterwards is what says the handler returned.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            ListCount = 2,
            RenameThrows = new MuninExplorerRateLimitedException()
        };
        var cut = RenderView(client);

        cut.FindAll("input[type=text]")[1].Change("Hjertet mitt");
        await PressAsync(cut, "Gi nytt navn");

        // The throttle's own words: the catalogue is up and the reader is being asked to wait,
        // which the ordinary "try again shortly" does not say.
        Assert.Contains("for mange forespørsler", cut.Markup);
        Assert.DoesNotContain("Kunne ikke endre listen", cut.Markup);

        var before = client.VariablesCalls;
        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));

        Assert.True(client.VariablesCalls > before, "the view stopped answering after the failure");
    }

    [Fact]
    public async Task View_WhenTheDeleteFails_ThenTheOrdinaryMessageIsShownAndTheListStays()
    {
        // The other half of the pair above: not a throttle, so the reader is told to try again
        // rather than to wait, and the list is still on screen because nothing on the server
        // changed.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            ListCount = 2,
            DeleteThrows = new InvalidOperationException("the API is unreachable")
        };
        var cut = RenderView(client);

        await PressAsync(cut, "Slett listen");
        await PressAsync(cut, "Ja, slett listen");

        Assert.Contains("Kunne ikke endre listen", cut.Markup);
        Assert.Contains("Mine hjertevariabler", cut.Markup);

        var before = client.VariablesCalls;
        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));

        Assert.True(client.VariablesCalls > before, "the view stopped answering after the failure");
    }

    [Fact]
    public async Task View_WhenRemovingAVariableIsRateLimited_ThenItSaysSoAndTheViewIsStillWorking()
    {
        // Remove is one of the four verbs the limiter counts, and unguarded the 429 left the
        // event handler and took the circuit with it. The switch of list afterwards is what says
        // the handler returned - the sentence on its own would be there either way.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            ListCount = 2,
            RemoveThrows = new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30))
        };
        var cut = RenderView(client);

        await cut.InvokeAsync(() => cut.FindAll(".munin-explorer-dataitem-main button")[0].Click());

        Assert.Contains("for mange forespørsler", cut.Markup);
        Assert.DoesNotContain("Kunne ikke endre listen", cut.Markup);

        var before = client.VariablesCalls;
        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));

        Assert.True(client.VariablesCalls > before, "the view stopped answering after the failure");
    }

    [Fact]
    public async Task View_WhenRemovingAVariableFails_ThenTheOrdinaryMessageIsShownAndTheRowStays()
    {
        // The converse of the case above: not a throttle, so the reader is told to try again
        // rather than to wait, and the row is still there because nothing on the server changed.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            ListCount = 2,
            RemoveThrows = new InvalidOperationException("the API is unreachable")
        };
        var cut = RenderView(client);

        await cut.InvokeAsync(() => cut.FindAll(".munin-explorer-dataitem-main button")[0].Click());

        Assert.Contains("Kunne ikke endre listen", cut.Markup);
        Assert.DoesNotContain("for mange forespørsler", cut.Markup);
        Assert.Contains("Alder ved diagnose", cut.Markup);

        var before = client.VariablesCalls;
        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));

        Assert.True(client.VariablesCalls > before, "the view stopped answering after the failure");
    }

    [Fact]
    public async Task View_WhenRemovingAVariableIsDeclined_ThenItSaysSoAndTheRowStays()
    {
        // Not a throw: the API took the call and answered no - a 404 for a list that is no longer
        // the reader's. The holder runs no staleness guard on this path, so a false is always an
        // answer worth passing on, which is what the silent handler did not do.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            ListCount = 2,
            RemoveIsDeclined = true
        };
        var cut = RenderView(client);

        await cut.InvokeAsync(() => cut.FindAll(".munin-explorer-dataitem-main button")[0].Click());

        Assert.Contains("Kunne ikke endre listen", cut.Markup);
        Assert.DoesNotContain("for mange forespørsler", cut.Markup);
        Assert.Contains("Alder ved diagnose", cut.Markup);

        // Acceptance 3. The sentence would be on screen either way; only a new interaction says
        // the handler returned rather than took the circuit with it.
        var before = client.VariablesCalls;
        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));

        Assert.True(client.VariablesCalls > before, "the view stopped answering after the failure");
    }

    [Fact]
    public async Task View_WhenADeclinedRemovalIsTriedAgainAndAccepted_ThenTheRowGoesAndTheAlertEmpties()
    {
        // The other half of acceptance 2: the message belongs to the removal that failed, not to
        // the view. A handler that set it and never cleared it would leave the reader told their
        // list could not be changed while looking at the row that just left it.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { RemoveIsDeclined = true };
        var cut = RenderView(client);

        await cut.InvokeAsync(() => cut.FindAll(".munin-explorer-dataitem-main button")[0].Click());
        Assert.Contains("Kunne ikke endre listen", cut.Markup);

        client.RemoveIsDeclined = false;
        await cut.InvokeAsync(() => cut.FindAll(".munin-explorer-dataitem-main button")[0].Click());

        Assert.Equal(2, client.RemoveCalls);
        Assert.DoesNotContain("Alder ved diagnose", cut.Markup);
        Assert.DoesNotContain("Kunne ikke endre listen", cut.Markup);
    }

    [Fact]
    public async Task View_WhenDeleteIsPressed_ThenNothingGoesUntilItIsConfirmed()
    {
        // Acceptance 4. A list can have taken a long time to build and the API offers no undo, so
        // the first press only asks.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2 };
        var cut = RenderView(client);

        var armed = cut.Find("button[aria-expanded]").Id;

        await PressAsync(cut, "Slett listen");

        Assert.Equal(0, client.DeleteCalls);
        Assert.Contains("Slett denne listen?", cut.Markup);

        // The same control, still there, now saying what a second press does. Swapping it for a
        // different one would drop the focus of whoever just pressed it to <body>.
        Assert.Equal(armed, cut.Find("button[aria-expanded='true']").Id);

        await PressAsync(cut, "Avbryt");

        Assert.Equal(0, client.DeleteCalls);
        Assert.Equal(armed, cut.Find("button[aria-expanded='false']").Id);
        Assert.Contains("Mine hjertevariabler", cut.Markup);
    }

    [Fact]
    public async Task View_WhenTheShownListChanges_ThenAnArmedConfirmationDoesNotGoWithIt()
    {
        // Two mounts share one holder, so another surface can move the active list under this one.
        // A confirmation armed on the list that has left would delete a list the reader never
        // looked at, which is the one press in this view with nothing behind it to undo.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2 };
        var cut = RenderView(client);

        await PressAsync(cut, "Slett listen");
        Assert.Contains("Slett denne listen?", cut.Markup);

        var state = Services.GetRequiredService<VariableListState>();
        await cut.InvokeAsync(() => state.SetActiveListAsync(ListClient.SecondListId));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));

        Assert.DoesNotContain("Slett denne listen?", cut.Markup);
        Assert.Equal(0, client.DeleteCalls);
    }

    [Fact]
    public async Task View_WhenAVariableGoesWhileARenameIsInFlight_ThenTheRowStillLeaves()
    {
        // The rename is allowed one notification without a page read, because a rename cannot change
        // what is in the list. An allowance held for the whole call rather than spent on the first
        // notification to arrive swallows the removal's as well, and the row stays on screen.
        var item = Item("Alder ved diagnose", "V_BDR.ALDER");
        VariableListState state = null!;

        var client = new ListClient(item)
        {
            ListCount = 2,
            DuringRename = () => state.RemoveVariablesAsync(ListId, [item.VariableId])
        };

        var cut = RenderView(client);
        state = Services.GetRequiredService<VariableListState>();

        cut.FindAll("input[type=text]")[1].Change("Hjertet mitt");
        await PressAsync(cut, "Gi nytt navn");

        Assert.Contains("Hjertet mitt", cut.Markup);
        Assert.DoesNotContain("Alder ved diagnose", cut.Markup);
    }

    [Fact]
    public async Task View_WhenAListIsDeleted_ThenItLeavesThePicker()
    {
        // Acceptance 2. Three lists so the picker outlives the deletion — with two it disappears
        // along with the choice, and the case would pass without the entry ever being removed.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 3 };
        var cut = RenderView(client);

        Assert.Equal(3, cut.FindAll("select option").Count);

        await PressAsync(cut, "Slett listen");
        await PressAsync(cut, "Ja, slett listen");

        Assert.Equal(1, client.DeleteCalls);

        var options = cut.FindAll("select option").Select(o => o.TextContent.Trim()).ToList();

        Assert.Equal(2, options.Count);
        Assert.DoesNotContain("Mine hjertevariabler", options);
    }

    [Fact]
    public async Task View_WhenTheActiveListIsDeleted_ThenAnotherBecomesActiveAndTheDeadOneIsNotAskedFor()
    {
        // Acceptance 3, and the trap the bead names. ActiveListId went on pointing at the list just
        // deleted, so the view asked for its variables; the API answers null — "no such list of
        // yours" — and a view reading null as "empty" draws a table for a list that is gone.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2 };
        var cut = RenderView(client);

        var state = Services.GetRequiredService<VariableListState>();

        Assert.Equal(ListId, state.ActiveListId);

        var askedBefore = client.VariablesAskedFor.Count;

        await PressAsync(cut, "Slett listen");
        await PressAsync(cut, "Ja, slett listen");

        // The explorer's save button reads this same field, and would otherwise go on writing into
        // a list the API no longer has.
        Assert.Equal(ListClient.SecondListId, state.ActiveListId);
        Assert.DoesNotContain(ListId, client.VariablesAskedFor.Skip(askedBefore));

        Assert.Contains("Hjerte og kar", cut.Markup);
        Assert.DoesNotContain("Mine hjertevariabler", cut.Markup);
    }

    [Fact]
    public async Task View_WhenTheOnlyListIsDeleted_ThenNothingIsActiveAndTheReaderIsToldTheyHaveNone()
    {
        // The other half of "another list becomes active, or none". With nothing left to point at,
        // a view that kept the deleted id would show an empty table where the reader should be told
        // they have no lists.
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"));
        var cut = RenderView(client);

        var state = Services.GetRequiredService<VariableListState>();
        var askedBefore = client.VariablesAskedFor.Count;

        await PressAsync(cut, "Slett listen");
        await PressAsync(cut, "Ja, slett listen");

        Assert.Null(state.ActiveListId);
        Assert.DoesNotContain(ListId, client.VariablesAskedFor.Skip(askedBefore));
        Assert.Contains("ingen variabellister", cut.Markup);
    }

    [Fact]
    public void View_WhenItIsDrawn_ThenEveryClassNameHasARuleInTheHostStylesheet()
    {
        // The package ships no CSS: a name with no rule behind it renders unstyled in the host.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"), Orphan()));

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }
}
