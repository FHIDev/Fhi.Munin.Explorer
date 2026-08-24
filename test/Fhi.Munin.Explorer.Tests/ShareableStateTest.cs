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

    private IRenderedComponent<VariableExplorer> Render(
        IMuninExplorerClient client,
        Action<ComponentParameterCollectionBuilder<VariableExplorer>>? p = null)
    {
        Services.AddSingleton(client);
        return Render<VariableExplorer>(b => p?.Invoke(b));
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

        // A facet is a button here, not a checkbox — the one carrying its count.
        cut.FindAll(".munin-explorer-filters button").First(b => b.TextContent.Contains("(9)")).Click();

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
}
