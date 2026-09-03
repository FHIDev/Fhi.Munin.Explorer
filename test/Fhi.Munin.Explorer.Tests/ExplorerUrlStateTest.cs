using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// What a host relies on when it puts explorer state in its own address bar.
/// </summary>
public class ExplorerUrlStateTest
{
    /// <summary>Binds the two copies of this number so they cannot drift. (Fhi.Metadata-f3p6v)</summary>
    [Fact]
    public void DefaultPageSize_IsTheSameNumberTheComponentUses()
    {
        Assert.Equal(ExplorerUrlState.DefaultPageSize, new VariableSearch().PageSize);
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

    /// <summary>The open variable is what a reader most wants to send someone.</summary>
    [Fact]
    public void RoundTrip_WhenAVariableIsOpen_ThenItComesBackTheSame()
    {
        var variable = Guid.NewGuid();
        var state = new ExplorerUrlState { Search = "svelging", SelectedVariableId = variable };

        var query = state.ToQueryString();

        Assert.Contains("variabelId=" + variable, query, StringComparison.Ordinal);
        Assert.Equal(variable, ExplorerUrlState.Parse(query).SelectedVariableId);
    }

    /// <summary>
    /// A URL that kept the id after the panel was closed would send the next reader to a variable
    /// the sender was no longer looking at.
    /// </summary>
    [Fact]
    public void ToQueryString_WhenNoVariableIsOpen_ThenTheKeyIsNotWritten()
    {
        var state = new ExplorerUrlState { Search = "svelging" };

        Assert.Equal("search=svelging", state.ToQueryString());
    }

    [Theory]
    [InlineData("?variabelId=notaguid")]
    [InlineData("?variabelId=42")]
    public void Parse_WhenTheVariableIdIsNotOne_ThenNothingIsOpen(string query)
    {
        Assert.Null(ExplorerUrlState.Parse(query).SelectedVariableId);
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

    /// <summary>The component clamps what it sends but reports the raw value back, so a size it
    /// cannot honour would be written into the URL over a page the reader is not looking at.</summary>
    [Theory]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=-10")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=99999")]
    [InlineData("?pageSize=notanumber")]
    public void Parse_WhenThePageSizeIsOneTheExplorerCannotHonour_ThenTheDefaultStands(string query)
    {
        Assert.Equal(ExplorerUrlState.DefaultPageSize, ExplorerUrlState.Parse(query).PageSize);
    }

    [Theory]
    [InlineData("?pageSize=1", 1)]
    [InlineData("?pageSize=100", 100)]
    [InlineData("?pageSize=50", 50)]
    public void Parse_WhenThePageSizeIsInRange_ThenItIsKept(string query, int expected)
    {
        Assert.Equal(expected, ExplorerUrlState.Parse(query).PageSize);
    }

    /// <summary>A host testing membership must not miss a key because of its case.</summary>
    [Theory]
    [InlineData("Search")]
    [InlineData("PAGESIZE")]
    [InlineData("sortdir")]
    [InlineData("VariabelId")]
    public void QueryKeys_MatchesTheCasesParseAccepts(string key)
    {
        Assert.Contains(key, ExplorerUrlState.QueryKeys);
    }

    /// <summary>Bounded like the filter's own values, since Parse reads whatever a public URL carried.</summary>
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
    /// <remarks>
    /// The filter is set here, and was not until <c>Fhi.Metadata-zrcf4</c>. This type writes the
    /// facets through the filter's own <c>ToQueryString</c>, so with an empty filter the test
    /// passed while <c>QueryKeys</c> named none of them — and a host following it kept
    /// <c>?kildeIds=</c> as one of its own and had it written twice.
    /// </remarks>
    [Fact]
    public void QueryKeys_NamesEveryKeyToQueryStringCanWrite()
    {
        var state = new ExplorerUrlState
        {
            Filter = new VariableFilter
            {
                KildeIds = [Guid.NewGuid()],
                KildeType = "biobank",
                DataTypes = ["1"],
                HasKildekodeverk = true,
                DataFrom = new DateOnly(2010, 1, 1),
                IncludeHistorical = true,
            },
            Search = "x",
            Sort = SortField.Kilde,
            Direction = SortDirection.Descending,
            Page = 2,
            PageSize = 99,
            SelectedVariableId = Guid.NewGuid(),
        };

        var written = state.ToQueryString()
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=')[0]);

        Assert.All(written, key => Assert.Contains(key, ExplorerUrlState.QueryKeys));

        // Named rather than left to the loop above: this is the key added last, and the defect the
        // remark describes is what happens when a key Parse reads is missing from the set.
        Assert.Contains("variabelId", written, StringComparer.Ordinal);
        Assert.Contains("variabelId", ExplorerUrlState.ScalarQueryKeys);
    }

    /// <summary>A host's own parameters are not ours to erase.</summary>
    [Fact]
    public void Parse_WhenTheHostHasItsOwnParameters_ThenTheyAreIgnoredNotMisread()
    {
        var state = ExplorerUrlState.Parse("?utm_source=nyhetsbrev&search=alder&tab=variabler");

        Assert.Equal("alder", state.Search);
    }
}
