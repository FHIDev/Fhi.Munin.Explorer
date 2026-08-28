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

    [Fact]
    public void View_WhenItIsDrawn_ThenEveryClassNameHasARuleInTheHostStylesheet()
    {
        // The package ships no CSS: a name with no rule behind it renders unstyled in the host.
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER"), Orphan()));

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }
}
