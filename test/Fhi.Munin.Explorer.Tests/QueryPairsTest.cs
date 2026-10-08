using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Parsing;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The one query-string splitter and the three caps its callers hand it (Fhi.Metadata-eeuey.4.3).
/// </summary>
/// <remarks>
/// Each cap is proved through its caller's own entry point, because a caller passing the wrong
/// constant to the shared splitter is a defect no test of the splitter alone can see.
/// </remarks>
public class QueryPairsTest
{
    [Fact]
    public void Split_WhenTheQueryHasEmptyPairsAndALeadingQuestionMark_ThenOnlyRealPairsAreRead()
    {
        var pairs = QueryPairs.Split("?a=1&&b=2&", 10).ToList();

        Assert.Equal(["a=1", "b=2"], pairs.Select(pair => pair.Raw));
    }

    [Fact]
    public void Split_WhenATokenIsEscaped_ThenPlusIsASpaceAndPercentTwoBIsAPlus()
    {
        var pair = Assert.Single(QueryPairs.Split("ICD+10%2B=a%20b+c%2B", 10));

        Assert.Equal("ICD 10+", pair.Name);
        Assert.Equal("a b c+", pair.Value);
    }

    [Fact]
    public void Split_WhenAPairHasNoNameToSplitOff_ThenTheWholePairIsTheNameAndTheValueIsNull()
    {
        var pairs = QueryPairs.Split("flag&=orphan", 10).ToList();

        Assert.Equal(["flag", "=orphan"], pairs.Select(pair => pair.Name));
        Assert.All(pairs, pair => Assert.Null(pair.Value));
    }

    /// <summary>Empty pairs are not pairs, so they must not spend the cap either.</summary>
    [Fact]
    public void Split_WhenTheCapIsReached_ThenEmptyPairsDidNotCountTowardsIt()
    {
        var pairs = QueryPairs.Split("&&a=1&&&b=2&c=3", 2).ToList();

        Assert.Equal(["a=1", "b=2"], pairs.Select(pair => pair.Raw));
    }

    [Fact]
    public void ExplorerUrlStateParse_WhenTheSearchIsPastTheCap_ThenItIsNotRead()
    {
        var filler = string.Join('&', Enumerable.Repeat("x=1", ExplorerUrlState.MaxParameters - 1));

        Assert.Equal("ALS", ExplorerUrlState.Parse(filler + "&search=ALS").Search);
        Assert.Null(ExplorerUrlState.Parse(filler + "&x=1&search=ALS").Search);
    }

    [Fact]
    public void VariableFilterParse_WhenAFacetIsPastTheCap_ThenItIsNotRead()
    {
        var filler = string.Join('&', Enumerable.Repeat("x=1", VariableFilter.MaxParameters - 1));

        Assert.Equal("Tekst", Assert.Single(VariableFilter.Parse(filler + "&datatypes=Tekst").DataTypes));
        Assert.Empty(VariableFilter.Parse(filler + "&x=1&datatypes=Tekst").DataTypes);
    }

    /// <summary>
    /// What the mirror reads is held for the circuit's life and written back into every link, so
    /// the bound covers carried pairs as well as owned ones.
    /// </summary>
    [Fact]
    public void UrlMirror_WhenTheQueryIsPastTheCap_ThenNeitherOwnedNorCarriedPairsBeyondItAreKept()
    {
        var filler = string.Join('&', Enumerable.Repeat("host=1", UrlMirror.MaxPairs - 1));
        var address = new Uri("https://host.example/side?" + filler + "&kilde=a&carried=b&kilde=c");

        var mirror = new UrlMirror(address, new RefusingJsRuntime(new InvalidOperationException()),
            name => name == "kilde");

        Assert.Equal(["a"], mirror.Values("kilde"));
        Assert.Equal("/side?" + filler, mirror.Address(""));
    }

    /// <summary>The cap must not cost the mirror its byte-for-byte promise to the host's own keys.</summary>
    [Fact]
    public void UrlMirror_WhenAParameterIsNotOwned_ThenItIsReEmittedExactlyAsItArrived()
    {
        var address = new Uri("https://host.example/side?ref=a+b%2Bc%20d&kilde=x&tom&=bare");

        var mirror = new UrlMirror(address, new RefusingJsRuntime(new InvalidOperationException()),
            name => name == "kilde");

        Assert.Equal("/side?ref=a+b%2Bc%20d&tom&=bare&kilde=y", mirror.Address("kilde=y"));
    }
}
