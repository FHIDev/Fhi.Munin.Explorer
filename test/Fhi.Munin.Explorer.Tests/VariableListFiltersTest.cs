using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The saved-list tab's own filter panel: which kilder the list draws from, and what a tick does
/// to the rows in the other column.
/// </summary>
/// <remarks>
/// Every fixture here that is about the tally is longer than one page, deliberately. The endpoint
/// pages, so a facet built from the page on screen and one built from the whole list answer a short
/// list identically — a test that could not tell them apart is the whole reason this was still
/// undone. Fhi.Metadata-uiqfs made the same mistake with the variable count.
/// </remarks>
public class VariableListFiltersTest : BunitContext
{
    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid Kreftregisteret = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Reseptregisteret = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Årsaksregisteret = new("aaaaaaaa-0000-0000-0000-000000000003");

    private static string NameOf(Guid kildeId) =>
        kildeId == Kreftregisteret ? "Kreftregisteret"
        : kildeId == Reseptregisteret ? "Reseptregisteret"
        : "Årsaksregisteret";

    private static VariableListItem Item(Guid kildeId, int n) => new()
    {
        VariableId = Guid.NewGuid(),
        AddedAt = DateTimeOffset.UtcNow,
        VariableName = $"{NameOf(kildeId)} {n}",
        VariableCode = $"V{n}",
        KildeId = kildeId,
        KildeName = NameOf(kildeId),
    };

    /// <summary>A list holding <paramref name="counts"/> variables of each named kilde.</summary>
    private static VariableListItem[] List(params (Guid Kilde, int Count)[] counts) =>
        [.. counts.SelectMany(c => Enumerable.Range(1, c.Count).Select(n => Item(c.Kilde, n)))];

    private sealed class ListClient(params VariableListItem[] items) : EmptyMuninExplorerClient
    {
        private readonly List<VariableListItem> _items = [.. items];

        /// <summary>What each read narrowed by, oldest first.</summary>
        public List<IReadOnlyList<Guid>> Narrowings { get; } = [];

        /// <summary>The pages asked for, so a narrowing that forgot to go back to page 1 shows.</summary>
        public List<int> PagesAsked { get; } = [];

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = ListId, Name = "Mine hjertevariabler", VariableCount = _items.Count }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            Narrowings.Add(kildeIds is null ? [] : [.. kildeIds]);
            PagesAsked.Add(page);

            // Narrowed, then counted, then cut — the order the API uses. A fake that sieved the
            // page it had already cut would answer a whole-list total for a narrowed read, and
            // every assertion below about the pager would pass against the wrong component.
            var matching = kildeIds is { Count: > 0 }
                ? _items.Where(i => i.KildeId is { } k && kildeIds.Contains(k)).ToList()
                : _items;

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. matching.Skip((page - 1) * pageSize).Take(pageSize)],
                TotalCount = matching.Count,
                PageNumber = page,
                Size = pageSize,
                TotalPages = Math.Max(1, (int)Math.Ceiling(matching.Count / (double)pageSize)),
            });
        }
    }

    /// <summary>
    /// Both halves on one circuit, which is the only way either is worth testing: the panel and the
    /// rows are separate components in separate grid columns and meet through the shared holder.
    /// </summary>
    private sealed record Both(
        IRenderedComponent<VariableListFilters> Filters,
        IRenderedComponent<VariableListView> View);

    private Both RenderBoth(ListClient client, string language = "no", int pageSize = 25)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        var view = Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, language)
            .Add(c => c.PageSize, pageSize));

        var filters = Render<VariableListFilters>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, language));

        return new Both(filters, view);
    }

    private static IReadOnlyList<string> Facets(IRenderedComponent<VariableListFilters> cut) =>
        [.. cut.FindAll(".munin-explorer-filters label").Select(l => l.TextContent.Trim())];

    private static IReadOnlyList<AngleSharp.Dom.IElement> Boxes(IRenderedComponent<VariableListFilters> cut) =>
        [.. cut.FindAll(".munin-explorer-filters input[type=checkbox]")];

    private static int RowCount(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("table.munin-explorer-data-list tbody tr").Count;

    // -----------------------------------------------------------------------
    // The tally.

    [Fact]
    public void Kilder_WhenTheListIsLongerThanAPage_ThenTheCountsAreTheWholeListsAndNotThePages()
    {
        // 60 variables read 25 at a time. A tally taken from the page on screen would say
        // Kreftregisteret (25) and name neither of the others — which is a perfectly plausible
        // sidebar, and the reason this fixture is three pages rather than one.
        var client = new ListClient(List(
            (Kreftregisteret, 40),
            (Reseptregisteret, 15),
            (Årsaksregisteret, 5)));

        var cut = RenderBoth(client);

        Assert.Equal(
            ["Kreftregisteret (40)", "Reseptregisteret (15)", "Årsaksregisteret (5)"],
            Facets(cut.Filters));
    }

    [Fact]
    public void Kilder_WhenTheListOutrunsTheWalksOwnPage_ThenTheLaterPagesAreTalliedToo()
    {
        // The walk reads 1000 at a time, so 60 variables prove only that the tally is not the
        // VIEW's page. This one outruns the WALK's page as well: everything from the second kilde
        // sits past row 1000, so a tally that stopped after the first request would name one kilde
        // and undercount it. 1200 rows in the fake, 25 on screen.
        var client = new ListClient(List(
            (Kreftregisteret, 1100),
            (Reseptregisteret, 100)));

        var cut = RenderBoth(client);

        Assert.Equal(
            ["Kreftregisteret (1100)", "Reseptregisteret (100)"],
            Facets(cut.Filters));
    }

    [Fact]
    public void Kilder_WhenTheListIsRead_ThenTheyStandInTheCataloguesOrderAndNotTheReaders()
    {
        // Å after R, whoever is reading: these are names the catalogue stores in Norwegian, so an
        // English reader's sidebar must not be sorted into a different order from a colleague's.
        var client = new ListClient(List(
            (Årsaksregisteret, 1),
            (Kreftregisteret, 1),
            (Reseptregisteret, 1)));

        var cut = RenderBoth(client, language: "en");

        Assert.Equal(
            ["Kreftregisteret (1)", "Reseptregisteret (1)", "Årsaksregisteret (1)"],
            Facets(cut.Filters));
    }

    [Fact]
    public void Kilder_WhenTheListHoldsNone_ThenItSaysSo()
    {
        Assert.Contains(
            "Ingen kilder i listen ennå.",
            RenderBoth(new ListClient()).Filters.Markup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Kilder_WhenTheListHoldsNoneAndTheReaderIsEnglish_ThenItSaysSoInEnglish()
    {
        Assert.Contains(
            "No sources in the list yet.",
            RenderBoth(new ListClient(), language: "en").Filters.Markup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Kilder_WhenAnEntrysVariableHasLeftTheCatalogue_ThenItNamesNoKilde()
    {
        // An orphan has no kilde to be filed under, and inventing one would put a checkbox in the
        // panel that narrows the list to nothing.
        var client = new ListClient(
            Item(Kreftregisteret, 1),
            new VariableListItem { VariableId = Guid.NewGuid(), AddedAt = DateTimeOffset.UtcNow });

        var cut = RenderBoth(client);

        Assert.Equal(["Kreftregisteret (1)"], Facets(cut.Filters));
    }

    // -----------------------------------------------------------------------
    // The narrowing.

    [Fact]
    public void Tick_WhenAKildeIsChosen_ThenTheRowsAndTheTotalFollowIt()
    {
        var client = new ListClient(List(
            (Kreftregisteret, 40),
            (Reseptregisteret, 15)));

        var cut = RenderBoth(client);

        Assert.Equal(25, RowCount(cut.View));

        Boxes(cut.Filters)[1].Change(true);

        // 15 rows on one page, from an API read that was actually narrowed — the last narrowing
        // carries the kilde, so this is not a component sieving what it already had.
        Assert.Equal(15, RowCount(cut.View));
        Assert.Equal([Reseptregisteret], client.Narrowings[^1]);
    }

    [Fact]
    public void Tick_WhenASecondKildeIsChosen_ThenTheTwoAreAUnion()
    {
        // Checkboxes, so several ticks widen rather than narrow further. Two kilder of 15 and 5
        // give 20 rows, not the 0 an intersection would.
        var client = new ListClient(List(
            (Kreftregisteret, 40),
            (Reseptregisteret, 15),
            (Årsaksregisteret, 5)));

        var cut = RenderBoth(client, pageSize: 100);

        Boxes(cut.Filters)[1].Change(true);
        Boxes(cut.Filters)[2].Change(true);

        Assert.Equal(20, RowCount(cut.View));
        Assert.Equal([Reseptregisteret, Årsaksregisteret], [.. client.Narrowings[^1].Order()]);
    }

    [Fact]
    public void Tick_WhenTheReaderIsPastTheNarrowedEnd_ThenTheyAreTakenBackToPageOne()
    {
        // Page 3 of a 60-variable list, then a tick that leaves 15. Without the reset the next read
        // asks for page 3 of one page and hands the reader an empty table with nothing to say why.
        var client = new ListClient(List(
            (Kreftregisteret, 45),
            (Reseptregisteret, 15)));

        var cut = RenderBoth(client);

        cut.View.FindAll("nav[aria-label], .munin-explorer-pagination button")
            .First(b => b.TextContent.Trim() == "3")
            .Click();

        Assert.Equal(3, client.PagesAsked[^1]);

        Boxes(cut.Filters)[1].Change(true);

        Assert.Equal(1, client.PagesAsked[^1]);
        Assert.Equal(15, RowCount(cut.View));
    }

    [Fact]
    public void Tick_WhenItIsPressedAgain_ThenTheWholeListIsBack()
    {
        var client = new ListClient(List((Kreftregisteret, 40), (Reseptregisteret, 15)));

        var cut = RenderBoth(client, pageSize: 100);

        Boxes(cut.Filters)[1].Change(true);
        Assert.Equal(15, RowCount(cut.View));

        Boxes(cut.Filters)[1].Change(false);

        Assert.Equal(55, RowCount(cut.View));
        Assert.Empty(client.Narrowings[^1]);
    }

    [Fact]
    public void ClearAll_WhenNothingIsTicked_ThenThereIsNoButtonToPress()
    {
        // A control that unticks nothing tells the reader they have narrowed something.
        var cut = RenderBoth(new ListClient(List((Kreftregisteret, 3))));

        Assert.Empty(cut.Filters.FindAll(".munin-explorer-filters button"));

        Boxes(cut.Filters)[0].Change(true);

        Assert.Single(cut.Filters.FindAll(".munin-explorer-filters button"));
    }

    [Fact]
    public void ClearAll_WhenItIsPressed_ThenEveryTickGoesAndTheRowsComeBack()
    {
        var client = new ListClient(List((Kreftregisteret, 40), (Reseptregisteret, 15)));

        var cut = RenderBoth(client, pageSize: 100);

        Boxes(cut.Filters)[1].Change(true);
        cut.Filters.Find(".munin-explorer-filters button").Click();

        Assert.Equal(55, RowCount(cut.View));
        Assert.DoesNotContain(Boxes(cut.Filters), b => b.HasAttribute("checked"));
    }

    [Fact]
    public void Tick_WhenTheListHasNoRowsUnderIt_ThenItDoesNotSayTheListIsEmpty()
    {
        // The tally is a moment older than the rows, so a kilde emptied in another tab can still
        // have a box. "Denne listen er tom" over a list of 55 would send the reader looking for
        // variables they have not lost.
        var client = new ListClient(List((Kreftregisteret, 55)));

        var cut = RenderBoth(client);

        Assert.Empty(cut.Filters.FindAll(".munin-explorer-filters p"));

        // A kilde the list does not hold, reached the way a stale tick would reach it.
        var state = Services.GetRequiredService<VariableListState>();
        cut.Filters.InvokeAsync(() => state.ToggleKildeFilter(Reseptregisteret));

        Assert.Contains("Ingen variabler fra de valgte kildene.", cut.View.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Denne listen er tom.", cut.View.Markup, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // What the panel is, and is not.

    [Fact]
    public void Panel_WhenItIsDrawn_ThenItWearsOnlyNamesSomebodyAlreadyStyles()
    {
        // The package ships no CSS. `munin-explorer-filters` is what Stiler places in the grid's
        // filter track, and an invented name beside it would render at raw browser defaults.
        var cut = RenderBoth(new ListClient(List((Kreftregisteret, 2)))).Filters;

        Assert.Single(cut.FindAll(".munin-explorer-filters"));
        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Panel_WhenTheReaderIsSignedOut_ThenThereIsNothingAtAll()
    {
        // The view beside it renders nothing signed out, so a filter panel over it would be a
        // control with nothing behind it.
        Services.AddSingleton<IMuninExplorerClient>(new ListClient(List((Kreftregisteret, 2))));
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListFilters>(p => p.Add(c => c.IsAuthenticated, false));

        Assert.Equal("", cut.Markup.Trim());
    }

    [Fact]
    public void Panel_WhenTwoAreMountedOnOnePage_ThenTheirHeadingIdsDiffer()
    {
        // A host may put two explorers on one page, and two groups named from one id would both
        // take the first heading's words.
        Services.AddSingleton<IMuninExplorerClient>(new ListClient(List((Kreftregisteret, 2))));
        Services.AddScoped<VariableListState>();

        var first = Render<VariableListFilters>(p => p.Add(c => c.IsAuthenticated, true));
        var second = Render<VariableListFilters>(p => p.Add(c => c.IsAuthenticated, true));

        Assert.NotEqual(
            first.Find("[role=group]").GetAttribute("aria-labelledby"),
            second.Find("[role=group]").GetAttribute("aria-labelledby"));
    }
}
