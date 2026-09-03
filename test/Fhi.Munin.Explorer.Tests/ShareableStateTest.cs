using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

using static Fhi.Munin.Explorer.Tests.SortHeader;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The shareable-state contract: the component holds no URL, so everything a link would carry has
/// to be readable and writable by the host.
/// </summary>
/// <remarks>
/// This is the part of the component with no visible symptom when it breaks. A missing callback
/// looks like nothing at all until someone sends a colleague a link and it opens on the wrong list.
/// </remarks>
public class ShareableStateTest : BunitContext
{
    private static VariableSummary Row(string name) => new()
    {
        Id = Guid.NewGuid(),
        Code = name,
        PreferredTerm = name,
        KildeName = "Als registeret",
    };

    /// <summary>A client that pages a fixed set, and answers honestly when asked past the end.</summary>
    /// <remarks>
    /// Honestly, not helpfully: asked for page 40 of 3 it returns page 40 and no rows, which is what
    /// the real Explorer API does. A stub that clamped would have hidden the bug this fixture exists
    /// to pin.
    /// </remarks>
    private sealed class PagingClient(int pages, int perPage = 2) : EmptyMuninExplorerClient
    {
        public int LastPage { get; private set; }

        public SortField LastSort { get; private set; }

        public SortDirection LastDirection { get; private set; }

        public int Calls { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            LastPage = page;
            LastSort = sort;
            LastDirection = direction;
            Calls++;

            var rows = page >= 1 && page <= pages
                ? Enumerable.Range(1, perPage).Select(i => Row($"p{page}v{i}")).ToArray()
                : [];

            return Task.FromResult(new Page<VariableSummary>
            {
                Items = rows,
                TotalCount = pages * perPage,
                PageNumber = page,
                Size = perPage,
                TotalPages = pages,
            });
        }
    }

    private IRenderedComponent<VariableSearch> Render(
        IMuninExplorerClient client,
        Action<ComponentParameterCollectionBuilder<VariableSearch>>? p = null)
    {
        Services.AddSingleton(client);
        return Render<VariableSearch>(b => p?.Invoke(b));
    }

    [Fact]
    public void Restore_WhenALinkCarriesAPage_ThenThatPageIsFetchedRatherThanTheFirst()
    {
        var client = new PagingClient(pages: 5);

        Render(client, b => b.Add(c => c.Page, 3));

        Assert.Equal(3, client.LastPage);
    }

    [Fact]
    public void Restore_WhenALinkCarriesASort_ThenTheListArrivesInThatOrder()
    {
        var client = new PagingClient(pages: 2);

        Render(client, b => b
            .Add(c => c.Sort, SortField.Kilde)
            .Add(c => c.Direction, SortDirection.Descending));

        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Equal(SortDirection.Descending, client.LastDirection);
    }

    [Fact]
    public void Restore_WhenTheLinkOutlivedTheResultSet_ThenItLandsOnTheLastRealPage()
    {
        // Someone shares page 40, then a filter is tightened or rows are unpublished. The API
        // answers page 40 of 3 with no rows, which is true and useless: an empty list under
        // "Side 40 av 3", with a pager whose Next is already at the end.
        var client = new PagingClient(pages: 3);
        var reported = new List<int>();

        var cut = Render(client, b => b
            .Add(c => c.Page, 40)
            .Add(c => c.PageChanged, reported.Add));

        Assert.Equal(3, client.LastPage);
        Assert.NotEmpty(cut.FindAll(".munin-explorer-data-list__item"));

        // And the host is told, so a URL still saying 40 is corrected rather than left to mislead
        // the next person it is sent to.
        Assert.Equal(3, reported[^1]);
    }

    [Fact]
    public void Restore_WhenThePageIsWithinRange_ThenNoCorrectingFetchIsMade()
    {
        // The correction costs a second round trip, so it must not happen on the ordinary path.
        var client = new PagingClient(pages: 5);

        Render(client, b => b.Add(c => c.Page, 2));

        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public void Sort_WhenTheReaderReordersTheList_ThenTheHostIsToldBothHalves()
    {
        var client = new PagingClient(pages: 2);
        SortField? sort = null;
        SortDirection? direction = null;

        var cut = Render(client, b => b
            .Add(c => c.SortChanged, f => sort = f)
            .Add(c => c.DirectionChanged, d => direction = d));

        ClickSort(cut, "Kilde");

        Assert.Equal(SortField.Kilde, sort);
        Assert.Equal(SortDirection.Ascending, direction);
    }

    /// <summary>Answers every fetch, except the ones it is told to refuse.</summary>
    private sealed class FlakyClient(int pages, int perPage = 2) : EmptyMuninExplorerClient
    {
        /// <summary>Set to make the next single fetch fail, then clear itself.</summary>
        public bool FailNext { get; set; }

        public int LastPage { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;

                throw new HttpRequestException("nede");
            }

            LastPage = page;

            return Task.FromResult(new Page<VariableSummary>
            {
                Items = [.. Enumerable.Range(1, perPage).Select(i => Row($"p{page}v{i}"))],
                TotalCount = pages * perPage,
                PageNumber = page,
                Size = perPage,
                TotalPages = pages,
            });
        }

        // One facet, so there is something in the sidebar to tick. Without it the filter panel has
        // no controls and the narrowing cannot be attempted at all.
        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new FilterOptions { DataTypes = [new() { Value = "1", Count = 9 }] });
    }

    [Fact]
    public void Sort_WhenTheReorderNeverArrives_ThenTheHostIsNotToldThePageMoved()
    {
        // What this pins is the host-facing half: a failed sort reports nothing, so a mirrored URL
        // still points at the page whose rows are still on screen.
        //
        // The component also rolls its own page back, and that part is deliberately not asserted
        // here, because it has no consequence this test could observe. A failed fetch takes the
        // pager out of the document, so there is no page turn to try and no caption to read; and
        // every route back out — searching, sorting again, narrowing — resets the page to 1 anyway,
        // so the internal divergence converges before anything can see it. The rollback stays
        // because the file's own invariant is that internal state describes what is on screen, and
        // an untested invariant is still worth holding. Claiming a test for it would not be.
        var client = new FlakyClient(pages: 5);
        var reported = new List<int>();

        var cut = Render(client, b => b
            .Add(c => c.Page, 3)
            .Add(c => c.PageChanged, reported.Add));

        client.FailNext = true;
        ClickSort(cut, "Kilde");

        Assert.Equal([3], reported);
    }

    [Fact]
    public void Filter_WhenTheNarrowingNeverArrives_ThenThePageStaysWhereTheRowsAre()
    {
        // Same invariant from the other side. The filter is rolled back, so the rows on screen are
        // still page 3 of the unnarrowed set; reporting page 1 would take the page out of the host's
        // URL over a narrowing that never happened.
        var client = new FlakyClient(pages: 5);
        var reported = new List<int>();

        var cut = Render(client, b => b
            .Add(c => c.Page, 3)
            .Add(c => c.PageChanged, reported.Add));

        client.FailNext = true;

        // Any facet value will do — the one carrying its count.
        cut.FindAll(".munin-explorer-filters li > label")
           .First(l => l.TextContent.Contains("(9)", StringComparison.Ordinal))
           .QuerySelector("input")!
           .Change(true);

        Assert.Equal(3, reported[^1]);
    }

    [Fact]
    public void Page_WhenASearchRenumbersTheResults_ThenTheHostIsToldThePageWentBackToOne()
    {
        // The easy one to miss. A host that only heard about page turns would keep page=7 in a URL
        // whose result set no longer has seven pages, and the link would open on nothing.
        var client = new PagingClient(pages: 9);
        var reported = new List<int>();

        var cut = Render(client, b => b
            .Add(c => c.Page, 7)
            .Add(c => c.PageChanged, reported.Add));

        Assert.Equal(7, client.LastPage);

        cut.Find("form").Submit();

        Assert.Equal(1, reported[^1]);
    }

    /// <summary>Pages a fixed total honestly, at whatever size it is asked for.</summary>
    /// <remarks>
    /// <see cref="PagingClient"/> answers a fixed number of rows whatever it is asked for, which is
    /// what the page tests want and the size tests cannot use: a client that ignores the size
    /// cannot tell a size that reached the API from one that never left the component.
    /// </remarks>
    private sealed class SizedClient(int total) : EmptyMuninExplorerClient
    {
        public List<int> Pages { get; } = [];

        public List<int> Sizes { get; } = [];

        public int LastPage => Pages[^1];

        public int LastPageSize => Sizes[^1];

        /// <summary>Set to make the next single fetch fail, then clear itself.</summary>
        public bool FailNext { get; set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;

                throw new HttpRequestException("nede");
            }

            Pages.Add(page);
            Sizes.Add(pageSize);

            var taken = Math.Clamp(total - ((page - 1) * pageSize), 0, pageSize);

            return Task.FromResult(new Page<VariableSummary>
            {
                Items = [.. Enumerable.Range(1, taken).Select(i => Row($"p{page}v{i}"))],
                TotalCount = total,
                PageNumber = page,
                Size = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            });
        }
    }

    /// <summary>The size control, which is one <c>&lt;select&gt;</c> and not three buttons.</summary>
    private static AngleSharp.Dom.IElement SizeControl(IRenderedComponent<VariableSearch> cut) =>
        cut.Find(".munin-explorer-pagination-size select");

    private static void ChooseSize(IRenderedComponent<VariableSearch> cut, string size) =>
        SizeControl(cut).Change(size);

    /// <summary>The sizes on offer, as the reader reads them.</summary>
    private static IReadOnlyList<string> SizeOptions(IRenderedComponent<VariableSearch> cut) =>
        [.. cut.FindAll(".munin-explorer-pagination-size option").Select(o => o.TextContent.Trim())];

    /// <summary>The size the control is describing, off the option carrying <c>selected</c>.</summary>
    /// <remarks>
    /// Read off the attribute rather than off AngleSharp's <c>SelectedIndex</c>, which falls back
    /// to the first option when none carries it and would report "10" for a control that is
    /// describing nothing.
    /// </remarks>
    private static string? Selected(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll(".munin-explorer-pagination-size option")
            .SingleOrDefault(o => o.HasAttribute("selected"))
            ?.TextContent.Trim();

    /// <summary>Where the pager says the reader is, as <c>"1/2"</c>.</summary>
    /// <remarks>
    /// Off the numbered run rather than a caption, which is where the position moved under
    /// Fhi.Metadata-ejcbi — the filled number is the page in force, and the run always ends on the
    /// last page. The separator is this helper's own; nothing on screen says "1/2". <c>Single</c>
    /// is the assertion: a pager with two filled numbers, or none, has stopped saying where the
    /// reader is.
    /// </remarks>
    private static string Position(IRenderedComponent<VariableSearch> cut)
    {
        var numbers = cut.FindAll("div.munin-explorer-pagination-pages > button");
        var current = numbers.Single(number => number.GetAttribute("aria-current") == "page");

        return $"{current.TextContent}/{numbers[^1].TextContent}";
    }

    [Fact]
    public void PageSize_WhenTheReaderChoosesFifty_ThenFiftyRowsArriveUnderATwoPagePager()
    {
        var client = new SizedClient(total: 60);

        var cut = Render(client);

        ChooseSize(cut, "50");

        Assert.Equal(50, client.LastPageSize);
        Assert.Equal(50, cut.FindAll(".munin-explorer-data-list__item").Count);
        Assert.Equal("1/2", Position(cut));
    }

    [Fact]
    public void PageSize_WhenTheReaderChoosesASize_ThenTheHostIsToldWhichOne()
    {
        var client = new SizedClient(total: 60);
        var reported = new List<int>();

        var cut = Render(client, b => b.Add(c => c.PageSizeChanged, reported.Add));

        // Not 20: that is the default, and choosing the size already in force is inert by design,
        // so a test pressing it would pass on a component that reported nothing at all.
        ChooseSize(cut, "50");

        Assert.Equal([50], reported);
    }

    [Fact]
    public void PageSize_WhenTheReaderChoosesTheSizeAlreadyInForce_ThenNothingIsFetchedOrReported()
    {
        var client = new SizedClient(total: 60);
        var reported = new List<int>();

        var cut = Render(client, b => b.Add(c => c.PageSizeChanged, reported.Add));

        var fetches = client.Sizes.Count;

        ChooseSize(cut, "20");

        Assert.Equal(fetches, client.Sizes.Count);
        Assert.Empty(reported);
    }

    [Fact]
    public void PageSize_WhenItChangesFromDeepInTheResult_ThenTheReaderLandsOnPageOne()
    {
        // The trap. Page 3 of 50-row pages is not the rows page 3 of 20-row pages was, so an
        // implementation that keeps the number passes every other test here and still moves the
        // reader somewhere arbitrary, with nothing on screen saying so.
        var client = new SizedClient(total: 300);
        var reported = new List<int>();

        var cut = Render(client, b => b
            .Add(c => c.Page, 3)
            .Add(c => c.PageChanged, reported.Add));

        Assert.Equal(3, client.LastPage);

        ChooseSize(cut, "50");

        Assert.Equal(1, client.LastPage);
        Assert.Equal("1/6", Position(cut));

        // And the host is told, or a mirrored URL would still say page=3 over the rows of page 1.
        Assert.Equal(1, reported[^1]);
    }

    [Theory]
    [InlineData(500, 100)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public void PageSize_WhenTheHostAsksOutsideTheApiRange_ThenItIsClampedRatherThanRefused(
        int asked, int reaching)
    {
        var client = new SizedClient(total: 300);

        Render(client, b => b.Add(c => c.PageSize, asked));

        Assert.Equal(reaching, client.LastPageSize);
    }

    [Fact]
    public void PageSize_WhenTheControlOffersItsSizes_ThenNoneOfThemBypassesTheClamp()
    {
        // The clamp is one expression in one place and the control reads through it, so the only
        // way past it is an offered size outside the range: a fourth option saying 250 would fetch
        // 100 rows and stay selected, which is the control lying about what it did.
        var client = new SizedClient(total: 300);

        var cut = Render(client);

        // Not an empty run: the sizes moved from three buttons into one select under
        // Fhi.Metadata-ejcbi, and a loop over a selector that now matches nothing passes without
        // asserting anything.
        Assert.NotEmpty(SizeOptions(cut));
        Assert.All(SizeOptions(cut), size => Assert.InRange(int.Parse(size), 1, 100));
    }

    [Fact]
    public async Task PageSize_WhenAValueTheControlNeverOfferedArrives_ThenItIsDroppedRatherThanFetched()
    {
        // Only reachable by editing the markup, which is why the handler does not trust what the
        // select hands it: clamping instead would fetch 100 rows and leave the control describing a
        // paging nobody chose. The event is raised directly rather than through ChooseSize, because
        // AngleSharp resolves an unknown value against the options before the handler sees it and
        // would deliver "10" — a size that IS offered, so the guard would never be reached and the
        // test would be asserting the ordinary path under this name.
        var client = new SizedClient(total: 300);
        var reported = new List<int>();

        var cut = Render(client, b => b.Add(c => c.PageSizeChanged, reported.Add));

        await SizeControl(cut).TriggerEventAsync(
            "onchange", new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "250" });

        // Not a fetch count: the await lets a still-settling initial render land, and a count is
        // then reporting timing rather than the guard. What 250 becomes if it is let through is the
        // clamp's 100 — so 100 reaching the wire, or being reported, or appearing in the control,
        // is the failure, and 250 never appears in any of them either way.
        Assert.DoesNotContain(100, client.Sizes);
        Assert.Empty(reported);
        Assert.Equal("20", Selected(cut));
        Assert.Equal(["10", "20", "50"], SizeOptions(cut));
    }

    [Fact]
    public void PageSize_WhenTheChoiceIsMirroredIntoALink_ThenReopeningItKeepsTheSize()
    {
        // The whole reason the callback is a requirement rather than a detail. Everything else the
        // reader can change survives a shared link; a picker without this would be the one piece of
        // state that silently does not.
        var client = new SizedClient(total: 300);
        var mirrored = 20;

        var cut = Render(client, b => b.Add(c => c.PageSizeChanged, size => mirrored = size));

        ChooseSize(cut, "50");

        Assert.Equal(50, mirrored);

        // Rebuilt from what the host wrote down, the way a link opens in someone else's browser.
        var reopenedAt = client.Sizes.Count;

        Render<VariableSearch>(b => b.Add(c => c.PageSize, mirrored));

        Assert.Equal(50, client.Sizes[reopenedAt]);
    }

    [Fact]
    public void PageSize_WhenTheChangeFailsAndIsRetried_ThenTheSizeTheReaderAskedForIsWhatArrives()
    {
        // The size has to travel in the replayed request. A failed change rolls it back to describe
        // the rows still on screen, so a retry reading the fields as they stand would fetch the OLD
        // size, succeed, and clear the error — reporting a change that never happened.
        var client = new SizedClient(total: 300);
        var reported = new List<int>();

        var cut = Render(client, b => b.Add(c => c.PageSizeChanged, reported.Add));

        client.FailNext = true;
        ChooseSize(cut, "50");

        Assert.Empty(reported);

        cut.FindAll("div[role='alert'][aria-live='assertive'] button")
            .Single(b => b.TextContent == "Prøv søket på nytt")
            .Click();

        Assert.Equal(50, client.LastPageSize);
        Assert.Equal([50], reported);

        // And the failure is gone from the alert region. The button stays, deliberately inert, so
        // that the element the reader just pressed is not taken out from under their focus.
        Assert.Empty(cut.FindAll("div[role='alert'][aria-live='assertive'] p.infobox"));
    }

    [Fact]
    public void Retry_WhileTheRetryIsRunning_ThenTheBoxSaysSoInsteadOfEmptying()
    {
        // The reader saw the sentence vanish while the button stayed, which reads as a control
        // with nothing to answer. The box cannot leave — the button inside it would go with it,
        // out from under the focus of whoever pressed it — so its words change instead.
        var client = new GatedClient(total: 300) { FailOn = 2, GateOn = 3 };

        var cut = Render(client, b => b.Add(c => c.Page, 2));

        ChooseSize(cut, "50");

        var box = cut.WaitForElement("div[role='alert'] p.infobox");

        Assert.DoesNotContain("Prøver igjen", box.TextContent);
        Assert.Null(box.GetAttribute("aria-busy"));

        cut.Find("div[role='alert'][aria-live='assertive'] button").Click();

        cut.WaitForAssertion(() =>
        {
            var busy = cut.Find("div[role='alert'] p.infobox");

            Assert.Equal("Prøver igjen …", busy.TextContent);
            Assert.Equal("true", busy.GetAttribute("aria-busy"));
        });

        // And the button is still there to be focused, which is the whole reason the box stayed.
        Assert.Single(cut.FindAll("div[role='alert'][aria-live='assertive'] button"));

        client.Release();
    }

    [Fact]
    public void Retry_WhenTheFiltersAreRefetching_ThenTheRowsFailureStillReadsAsAFailure()
    {
        // _loading is raised by a facets fetch as well as by a rows fetch, so "loading" on its own
        // does not mean the offer beside THIS sentence is being answered. Without the narrowing,
        // pressing the filters' retry while a rows failure stood rewrote the rows' sentence into
        // "trying again" — an answer to a question nobody had asked yet.
        var client = new GatedClient(total: 300) { FailOn = 2, FacetsFailOn = 1, FacetsGateOn = 2 };

        var cut = Render(client, b => b.Add(c => c.Page, 2));

        ChooseSize(cut, "50");

        var rowsFailure = cut.WaitForElement("div.munin-explorer-alert p.infobox").TextContent;

        Assert.DoesNotContain("Prøver igjen", rowsFailure);

        // The filters' own retry, which fetches facets and leaves the rows' message alone.
        cut.FindAll("div[role='alert'][aria-live='assertive'] button")
            .First(b => b.TextContent == "Prøv filtrene på nytt")
            .Click();

        Assert.Equal(
            rowsFailure,
            cut.FindAll("div.munin-explorer-alert p.infobox")[0].TextContent);

        client.ReleaseFacets();
    }

    [Fact]
    public void Retry_WhenAPageTurnFollowsASuccessfulRetry_ThenNothingClaimsToBeRetrying()
    {
        // The offer outlives its own answer on purpose — it is the focus anchor for whoever pressed
        // it — so "there is a failed request on file and a fetch is running" stays true long after
        // the retry finished. Derived from that, an ordinary page turn announced itself as a retry.
        var client = new GatedClient(total: 300) { FailOn = 2, GateOn = 4 };

        var cut = Render(client);

        ChooseSize(cut, "50");

        cut.WaitForElement("div[role='alert'][aria-live='assertive'] button").Click();

        // The rows are back and the offer is still there, inert, holding focus.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("div.munin-explorer-alert p.infobox")));

        cut.FindAll("div.munin-explorer-pagination-content > button")
            .First(b => b.TextContent == "Neste")
            .Click();

        Assert.DoesNotContain(
            "Prøver igjen",
            cut.Find("div[role='alert']").TextContent);

        client.Release();
    }

    [Fact]
    public void PageSize_WhenAScreenReaderMeetsTheControl_ThenItSaysWhatTheSizesAreFor()
    {
        // "20" is what the control shows and not what it means. Three buttons each had to repeat
        // the whole phrase, because a reader tabbing into the middle of a group never hears its
        // name; one select is named once, by the words already on screen beside it.
        var client = new SizedClient(total: 300);

        var cut = Render(client);

        Assert.Equal("Variabler per side", AccessibleName.Of(SizeControl(cut)));
        Assert.Equal(["10", "20", "50"], SizeOptions(cut));

        // The label is a real <label for>, which is what carries that name — an adjacent span
        // would put the same words on screen and name nothing.
        var label = cut.Find(".munin-explorer-pagination-size label");

        Assert.Equal(SizeControl(cut).Id, label.GetAttribute("for"));
    }

    [Fact]
    public void PageSize_WhenAHostSetsNoSize_ThenTwentyIsFetchedAndIsTheOneSelected()
    {
        // The default is one of the three offered, which is why it moved from 25 when the control
        // arrived: a control describing a size nobody can choose reads as broken. A host that wants
        // the old 25 has to ask for it.
        var client = new SizedClient(total: 300);

        var cut = Render(client);

        Assert.Equal(20, client.LastPageSize);
        Assert.Equal("20", Selected(cut));
    }

    [Fact]
    public void PageSize_WhenTheSizeInForceIsNotOneOfTheThree_ThenTheControlOffersItRatherThanLying()
    {
        // A host is free to set 30. Three buttons could say "none of us" by leaving every one
        // unpressed; a select has no such state — with no option for 30 the browser falls back to
        // showing the first, so the control would report 10 rows a page over rows built at 30.
        var client = new SizedClient(total: 300);

        var cut = Render(client, b => b.Add(c => c.PageSize, 30));

        Assert.Equal(["10", "20", "30", "50"], SizeOptions(cut));
        Assert.Equal("30", Selected(cut));

        ChooseSize(cut, "50");

        Assert.Equal("50", Selected(cut));
    }

    /// <summary>Answers on demand, so a fetch can be held in flight and looked at.</summary>
    private sealed class GatedClient(int total) : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource _gate = new();

        public int Calls { get; private set; }

        /// <summary>Which call throws, so the retry button has a failure to answer.</summary>
        public int FailOn { get; set; } = -1;

        /// <summary>Which call waits for <see cref="Release"/> before answering.</summary>
        public int GateOn { get; set; } = -1;

        private readonly TaskCompletionSource _facetGate = new();

        public int FacetCalls { get; private set; }

        /// <summary>Which facet call throws, so the filters get a failure of their own.</summary>
        public int FacetsFailOn { get; set; } = -1;

        /// <summary>Which facet call waits, so a facets fetch can be held in flight.</summary>
        public int FacetsGateOn { get; set; } = -1;

        public void Release() => _gate.TrySetResult();

        public void ReleaseFacets() => _facetGate.TrySetResult();

        public override async Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null,
            CancellationToken cancellationToken = default)
        {
            FacetCalls++;

            if (FacetCalls == FacetsFailOn)
            {
                throw new HttpRequestException("nede");
            }

            if (FacetCalls == FacetsGateOn)
            {
                await _facetGate.Task;
            }

            return new FilterOptions { DataTypes = [new() { Value = "1", Count = 9 }] };
        }

        public override async Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            if (Calls == FailOn)
            {
                throw new HttpRequestException("nede");
            }

            if (Calls == GateOn)
            {
                await _gate.Task;
            }

            var taken = Math.Clamp(total - ((page - 1) * pageSize), 0, pageSize);

            return new Page<VariableSummary>
            {
                Items = [.. Enumerable.Range(1, taken).Select(i => Row($"p{page}v{i}"))],
                TotalCount = total,
                PageNumber = page,
                Size = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            };
        }
    }

    /// <summary>The size control's availability — one value, not a run.</summary>
    /// <remarks>
    /// An <c>Assert.All</c> over a selector matching nothing goes green saying nothing, which is
    /// what this check did the moment the three buttons became one select.
    /// </remarks>
    private static string? SizeControlState(IRenderedComponent<VariableSearch> cut) =>
        SizeControl(cut).GetAttribute("aria-disabled");

    [Fact]
    public void PageSize_WhileTheFetchIsStillRunning_ThenTheSizeControlReadsAsInertRatherThanLive()
    {
        // SetPageSizeAsync drops a change that arrives mid-fetch, and until this it dropped it
        // behind a control that still looked live — so a reader waiting on a slow answer chooses
        // again, nothing happens, and nothing says why.
        var client = new GatedClient(total: 300) { GateOn = 2 };

        var cut = Render(client);

        Assert.Null(SizeControlState(cut));

        ChooseSize(cut, "50");

        // Waited for rather than read straight after the change: it sets _loading and asks for a
        // render, and whether that render has been applied by the next line is a matter of timing.
        // It held locally and did not on a slower CI runner.
        cut.WaitForAssertion(() => Assert.Equal("true", SizeControlState(cut)));

        client.Release();
        cut.WaitForAssertion(() => Assert.Null(SizeControlState(cut)));
    }

    [Fact]
    public void Retry_WhileTheRetryItStartedIsStillRunning_ThenItReadsAsInertRatherThanLive()
    {
        // Pre-existing behaviour, untested until now: FetchRowsAsync clears the offer and
        // re-renders before it awaits, so the button the reader just pressed says it is busy
        // instead of inviting a second press that RetryRowsAsync would drop.
        var client = new GatedClient(total: 300) { FailOn = 2, GateOn = 3 };

        var cut = Render(client, b => b.Add(c => c.Page, 2));

        ChooseSize(cut, "50");

        // The button arrives with the failure it answers, so wait for it rather than for the click
        // to have finished rendering by the time the next line runs.
        var retry = cut.WaitForElement("div[role='alert'][aria-live='assertive'] button");

        Assert.Null(retry.GetAttribute("aria-disabled"));

        retry.Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "true",
            cut.Find("div[role='alert'][aria-live='assertive'] button").GetAttribute("aria-disabled")));

        client.Release();
    }
}
