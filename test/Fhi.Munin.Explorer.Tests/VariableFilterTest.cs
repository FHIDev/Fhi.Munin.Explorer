using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The filter's query-string form, which is what makes a filtered search shareable.
/// </summary>
/// <remarks>
/// Two things depend on it and would drift apart silently without these: the client, which puts
/// the same string on the wire, and a host, which puts it in its own URL. The parameter names are
/// the Explorer API's own, so a rename here is a link that stops filtering rather than a build
/// error somewhere.
/// </remarks>
public class VariableFilterTest
{
    private static readonly Guid Kilde = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Delkilde = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Gruppe = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void ToQueryString_WhenNothingIsSelected_ThenItIsEmpty()
    {
        // An unfiltered search has to produce the URL it produced before filtering existed —
        // a public page's cache hit rate depends on it.
        Assert.Equal("", VariableFilter.None.ToQueryString());
        Assert.True(VariableFilter.None.IsEmpty);
        Assert.Equal(0, VariableFilter.None.ActiveCount);
    }

    [Fact]
    public void ToQueryString_WhenAFacetHasSeveralValues_ThenTheNameIsRepeatedPerValue()
    {
        // How the API binds a List<Guid>. Comma-joining them would bind as one malformed id.
        var filter = new VariableFilter { KildeIds = [Kilde, Delkilde] };

        Assert.Equal($"kildeIds={Kilde}&kildeIds={Delkilde}", filter.ToQueryString());
    }

    [Fact]
    public void ToQueryString_WhenEveryFacetIsSet_ThenTheApisOwnParameterNamesAreUsed()
    {
        var query = Everything().ToQueryString();

        // Spelled out rather than derived: these names are a contract with an API this repository
        // does not build, and getting one wrong is a filter that silently stops narrowing.
        Assert.Contains($"kildeIds={Kilde}", query, StringComparison.Ordinal);
        Assert.Contains("kildeType=sentraltHelseregister", query, StringComparison.Ordinal);
        Assert.Contains($"delkildeIds={Delkilde}", query, StringComparison.Ordinal);
        Assert.Contains($"datasamlingIds={Gruppe}", query, StringComparison.Ordinal);
        Assert.Contains($"variabelgruppeIds={Gruppe}", query, StringComparison.Ordinal);
        Assert.Contains($"filterIds={Kilde}", query, StringComparison.Ordinal);
        Assert.Contains("datatypes=1", query, StringComparison.Ordinal);
        Assert.Contains("helsefagligKodeverkReferanser=ICD-10", query, StringComparison.Ordinal);
        Assert.Contains("administrativtKodeverkOids=3402", query, StringComparison.Ordinal);
        Assert.Contains($"instrumentIds={Delkilde}", query, StringComparison.Ordinal);
        Assert.Contains("datakategorier=ehds-cat%3Abiobanks", query, StringComparison.Ordinal);
        Assert.Contains("harKildekodeverk=true", query, StringComparison.Ordinal);
        Assert.Contains("dataFrom=2010-01-01", query, StringComparison.Ordinal);
        Assert.Contains("dataTo=2025-06-30", query, StringComparison.Ordinal);
        Assert.Contains("includeHistorical=true", query, StringComparison.Ordinal);
    }

    [Fact]
    public void ToQueryString_WhenHistoricalIsOff_ThenTheParameterIsLeftOutAltogether()
    {
        // The API's own default, so sending it says nothing and lengthens every URL.
        Assert.DoesNotContain("includeHistorical", new VariableFilter { KildeIds = [Kilde] }.ToQueryString(),
                              StringComparison.Ordinal);
    }

    [Fact]
    public void ToQueryString_WhenAValueNeedsEscaping_ThenItIsEscaped()
    {
        var query = new VariableFilter { Categories = ["ehds-cat:population health"] }.ToQueryString();

        Assert.Equal("datakategorier=ehds-cat%3Apopulation%20health", query);
    }

    [Fact]
    public void Parse_WhenGivenWhatToQueryStringWrote_ThenTheFilterComesBackUnchanged()
    {
        // The round trip a deep link is: the host writes the query string into its URL and parses
        // it back on the next request. Anything that does not survive it is a filter that quietly
        // disappears when the link is shared.
        var original = Everything();

        Assert.Equal(original, VariableFilter.Parse(original.ToQueryString()));
    }

    [Fact]
    public void Parse_WhenTheQueryStringStillHasItsQuestionMark_ThenItIsAccepted()
    {
        // So a host can hand over Request.QueryString.Value without trimming it first.
        Assert.Equal(new VariableFilter { KildeIds = [Kilde] }, VariableFilter.Parse($"?kildeIds={Kilde}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("page=2&size=25")]
    public void Parse_WhenThereIsNothingItRecognises_ThenNothingIsFiltered(string? query)
    {
        // Query strings carry the host's own parameters too; the ones this does not know are not
        // its business, and it must not turn them into a filter.
        Assert.True(VariableFilter.Parse(query).IsEmpty);
    }

    [Fact]
    public void Parse_WhenAValueIsMalformed_ThenTheRestOfTheFilterSurvives()
    {
        // A URL is something a person can edit, and this renders on a public page. One broken id
        // must not throw the whole request away — nor be turned into a filter of its own.
        var filter = VariableFilter.Parse($"kildeIds=not-a-guid&kildeIds={Kilde}&dataFrom=neverday&kildeType=biobank");

        Assert.Equal([Kilde], filter.KildeIds);
        Assert.Null(filter.DataFrom);
        Assert.Equal("biobank", filter.KildeType);
    }

    [Fact]
    public void Parse_WhenAParameterNameIsCasedDifferently_ThenItIsStillRead()
    {
        // ASP.NET Core binds query parameters case-insensitively, so a hand-written link that
        // works against the API has to work here too.
        Assert.Equal("biobank", VariableFilter.Parse("KildeType=biobank").KildeType);
    }

    [Fact]
    public void Equals_WhenTwoFiltersNarrowTheSameWay_ThenTheyAreEqual()
    {
        // The record's own equality would compare the lists by reference, so a filter rebuilt from
        // the same ids would read as changed — and every caller asking "did this actually move"
        // would be told yes, on every render.
        var a = new VariableFilter { KildeIds = [Kilde], DataTypes = ["1"] };
        var b = new VariableFilter { KildeIds = [Kilde], DataTypes = ["1"] };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { DataTypes = ["2"] });
    }

    [Fact]
    public void ActiveCount_WhenSeveralFacetsAreSet_ThenEveryChosenValueCountsOnce()
    {
        var filter = new VariableFilter
        {
            KildeIds = [Kilde, Delkilde],
            KildeType = "biobank",
            DataTypes = ["1"],
            IncludeHistorical = true
        };

        Assert.Equal(5, filter.ActiveCount);
        Assert.False(filter.IsEmpty);
    }

    [Fact]
    public void Parse_WhenASpaceIsWrittenAsAPlus_ThenItIsReadAsASpace()
    {
        // ToQueryString writes %20, but a host may hand over a query string it did not write: an
        // HTML GET form, WebUtility.UrlEncode and QueryHelpers all write a space as +. Read
        // literally, "ICD+10" goes back to the API as ICD%2B10 and matches nothing, silently.
        Assert.Equal(["ICD 10"], VariableFilter.Parse("?helsefagligKodeverkReferanser=ICD+10").HelsefagligKodeverk);
    }

    [Fact]
    public void Parse_WhenAPlusIsEscaped_ThenItIsReadAsAPlus()
    {
        // The other half of the same rule, and the reason Decode replaces + before unescaping:
        // unescape first and %2B becomes a literal +, which the replace then turns into a space.
        // A value that really contains one — written by ToQueryString as %2B — would come back
        // different from what was linked and match nothing at the API, silently.
        Assert.Equal(["ICD+10"], VariableFilter.Parse("?helsefagligKodeverkReferanser=ICD%2B10").HelsefagligKodeverk);
    }

    [Fact]
    public void ToQueryString_WhenAValueContainsAPlus_ThenParseReadsBackTheSameValue()
    {
        // The round trip over the one character the two escapes disagree about. No fixture value
        // holds a +, so Everything()'s round-trip test cannot reach this on its own.
        var filter = new VariableFilter { HelsefagligKodeverk = ["ICD+10"], Categories = ["a+b"] };

        var parsed = VariableFilter.Parse(filter.ToQueryString());

        Assert.Equal(["ICD+10"], parsed.HelsefagligKodeverk);
        Assert.Equal(["a+b"], parsed.Categories);
    }

    [Fact]
    public void Parse_WhenAFacetHasMoreValuesThanASelectionCanHave_ThenTheRestAreDropped()
    {
        // The input is an anonymous URL and the result is held for the life of a circuit, scanned
        // per value on every render and put back on the outbound API URL on every fetch. Without a
        // cap, one crafted link is sustained server work — see the constants on Parse.
        var crafted = string.Join('&', Enumerable.Repeat($"kildeIds={Kilde}", 5_000));

        Assert.Equal(100, VariableFilter.Parse(crafted).KildeIds.Count);
    }

    [Fact]
    public void Parse_WhenAValueIsLongerThanAnyTheApiReports_ThenItIsDropped()
    {
        // The free-form facets have no format to fail on, so length is the only bound there is.
        var crafted = $"datatypes={new string('x', 500)}&kildeIds={Kilde}";

        var filter = VariableFilter.Parse(crafted);

        Assert.Empty(filter.DataTypes);
        Assert.Equal([Kilde], filter.KildeIds); // and the rest of the filter still survives
    }

    [Fact]
    public void ActiveCount_WhenAValueIsSetButNotSent_ThenItIsNotCounted()
    {
        // Counting and sending have to describe the same filter. A blank value narrows nothing, so
        // ToQuery leaves it out and Equals calls the filter unfiltered — counting it anyway leaves
        // a UI saying "Filtre (1)" over a live clear button whose press does nothing at all.
        var blank = new VariableFilter { KildeType = "", DataTypes = [""] };

        Assert.Equal(0, blank.ActiveCount);
        Assert.True(blank.IsEmpty);
        Assert.Equal(VariableFilter.None, blank);
        Assert.Equal("", blank.ToQueryString());
    }

    /// <summary>
    /// A name missing from this set is one an explorer component carries through and then writes a
    /// second time, so the URL ends up narrowing by a facet twice.
    /// </summary>
    [Fact]
    public void QueryKeys_NamesEveryKeyToQueryWrites_AndNothingItDoesNot()
    {
        var written = Everything().ToQuery().Select(pair => pair.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(written, name => Assert.Contains(name, VariableFilter.QueryKeys));
        Assert.Equal(written.Count, VariableFilter.QueryKeys.Count);
    }

    private static VariableFilter Everything() => new()
    {
        KildeIds = [Kilde],
        KildeType = "sentraltHelseregister",
        DelkildeIds = [Delkilde],
        DatasamlingIds = [Gruppe],
        VariabelgruppeIds = [Gruppe],
        FilterIds = [Kilde],
        DataTypes = ["1", "2"],
        HelsefagligKodeverk = ["ICD-10"],
        AdministrativtKodeverk = ["3402"],
        InstrumentIds = [Delkilde],
        Categories = ["ehds-cat:biobanks"],
        HasKildekodeverk = true,
        DataFrom = new DateOnly(2010, 1, 1),
        DataTo = new DateOnly(2025, 6, 30),
        IncludeHistorical = true
    };
}
