using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// What a host relies on when it puts explorer state in its own address bar.
/// </summary>
public class ExplorerUrlStateTest
{
    /// <summary>
    /// The reason this type exists rather than each host restating the number.
    /// </summary>
    /// <remarks>
    /// A host cannot read a component parameter's default, so every host that wanted to omit the
    /// page size from its URLs kept its own copy of it. Two copies far apart, and nothing failing
    /// when they drift: change the component and every host silently writes URLs that disagree with
    /// what the reader sees. This is the assertion that makes the copy safe to have removed.
    /// </remarks>
    [Fact]
    public void DefaultPageSize_IsTheSameNumberTheComponentUses()
    {
        Assert.Equal(ExplorerUrlState.DefaultPageSize, new VariableExplorer().PageSize);
    }

    [Fact]
    public void ToQueryString_WhenNothingIsChosen_ThenItIsEmpty()
    {
        Assert.Equal("", ExplorerUrlState.None.ToQueryString());
    }

    /// <summary>
    /// A link carries what someone chose, not a transcript of every setting.
    /// </summary>
    [Fact]
    public void ToQueryString_WhenOnlyDefaults_ThenNoKeyIsWritten()
    {
        var state = new ExplorerUrlState
        {
            Sort = SortField.Default,
            Direction = SortDirection.Ascending,
            Page = 1,
            PageSize = ExplorerUrlState.DefaultPageSize,
        };

        Assert.Equal("", state.ToQueryString());
    }

    [Fact]
    public void RoundTrip_WhenAReaderHasSearchedSortedAndPaged_ThenItComesBackTheSame()
    {
        var state = new ExplorerUrlState
        {
            Search = "alder ved diagnose",
            Sort = SortField.Kilde,
            Direction = SortDirection.Descending,
            Page = 3,
            PageSize = 50,
        };

        var back = ExplorerUrlState.Parse(state.ToQueryString());

        Assert.Equal(state.Search, back.Search);
        Assert.Equal(state.Sort, back.Sort);
        Assert.Equal(state.Direction, back.Direction);
        Assert.Equal(state.Page, back.Page);
        Assert.Equal(state.PageSize, back.PageSize);
    }

    /// <summary>The facets are the filter's own business, and must survive the trip through here.</summary>
    [Fact]
    public void RoundTrip_WhenTheFilterCarriesFacets_ThenTheySurvive()
    {
        var kilde = Guid.NewGuid();
        var state = new ExplorerUrlState
        {
            Filter = new VariableFilter { KildeIds = [kilde] },
            Search = "hjerte",
        };

        var back = ExplorerUrlState.Parse(state.ToQueryString());

        Assert.Equal([kilde], back.Filter.KildeIds);
        Assert.Equal("hjerte", back.Search);
    }

    [Theory]
    [InlineData("?search=alder")]
    [InlineData("search=alder")]
    public void Parse_WhetherOrNotTheQuestionMarkIsThere_ThenItReadsTheSame(string query)
    {
        Assert.Equal("alder", ExplorerUrlState.Parse(query).Search);
    }

    /// <summary>
    /// The trap a plain <c>Enum.TryParse</c> walks into.
    /// </summary>
    /// <remarks>
    /// It accepts any number, so <c>?sort=999</c> succeeds and yields a value no case covers, which
    /// then travels into the component and out to the API as a sort nobody defined.
    /// </remarks>
    [Theory]
    [InlineData("?sort=999")]
    [InlineData("?sort=notasort")]
    [InlineData("?sortDir=42")]
    public void Parse_WhenAnEnumIsNotOneTheTypeNames_ThenTheDefaultStands(string query)
    {
        var state = ExplorerUrlState.Parse(query);

        Assert.Equal(SortField.Default, state.Sort);
        Assert.Equal(SortDirection.Ascending, state.Direction);
    }

    [Theory]
    [InlineData("?page=0", 1)]
    [InlineData("?page=-5", 1)]
    [InlineData("?page=notanumber", 1)]
    public void Parse_WhenThePageIsNotOneAReaderCouldBeOn_ThenItFallsBackToTheFirst(string query, int expected)
    {
        Assert.Equal(expected, ExplorerUrlState.Parse(query).Page);
    }

    /// <summary>
    /// Parse reads whatever a public URL carried, so length is bounded like the filter's own values.
    /// </summary>
    [Fact]
    public void Parse_WhenTheSearchIsLongerThanAnyoneWouldType_ThenItIsDroppedRatherThanKept()
    {
        var query = "?search=" + new string('a', 5000);

        Assert.Null(ExplorerUrlState.Parse(query).Search);
    }

    /// <summary>A mangled link opens a working explorer rather than an error.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("?=nokey")]
    [InlineData("?search")]
    [InlineData("&&&")]
    public void Parse_WhenTheQueryIsRubbish_ThenItIsTheUntouchedState(string? query)
    {
        var state = ExplorerUrlState.Parse(query);

        Assert.Null(state.Search);
        Assert.Equal(1, state.Page);
        Assert.Equal(ExplorerUrlState.DefaultPageSize, state.PageSize);
    }

    /// <summary>
    /// A host needs to tell our parameters from its own before deciding what to do with the rest.
    /// </summary>
    [Fact]
    public void QueryKeys_NamesEveryKeyToQueryStringCanWrite()
    {
        var state = new ExplorerUrlState
        {
            Search = "x",
            Sort = SortField.Kilde,
            Direction = SortDirection.Descending,
            Page = 2,
            PageSize = 99,
        };

        var written = state.ToQueryString()
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=')[0]);

        Assert.All(written, key => Assert.Contains(key, ExplorerUrlState.QueryKeys));
    }

    /// <summary>A host's own parameters are not ours to erase.</summary>
    [Fact]
    public void Parse_WhenTheHostHasItsOwnParameters_ThenTheyAreIgnoredNotMisread()
    {
        var state = ExplorerUrlState.Parse("?utm_source=nyhetsbrev&search=alder&tab=variabler");

        Assert.Equal("alder", state.Search);
    }
}
