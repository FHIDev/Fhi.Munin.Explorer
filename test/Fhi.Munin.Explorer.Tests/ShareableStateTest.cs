using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.NotEmpty(cut.FindAll(".variable-data-list__item"));

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

        cut.FindAll("button").First(b => b.TextContent.Contains("Kilde")).Click();

        Assert.Equal(SortField.Kilde, sort);
        Assert.Equal(SortDirection.Ascending, direction);
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
