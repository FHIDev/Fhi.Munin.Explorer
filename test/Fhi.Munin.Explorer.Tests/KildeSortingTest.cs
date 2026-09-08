using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The order the kilde list is in, and the three ways it can be silently wrong.
/// </summary>
/// <remarks>
/// <para>
/// The first is the collation. <c>Fhi.Metadata-dpc6h</c> is a sort that ran under no pinned locale,
/// so its order was a property of the machine; the failure here would be quieter still, because a
/// Norwegian list sorted as English looks sorted — only æ, ø, å and the digraph aa are in the wrong
/// place, and only to a reader who knows where they belong. So the fixtures below carry all four
/// and the assertion is on the whole sequence.
/// </para>
/// <para>
/// The second is a missing value passing for a small one. A kilde with no established year and one
/// established in the year 0 are different things, and coalescing the first to the second files it
/// among real values where no reader can tell it apart. Both are asserted, in the same list, on
/// purpose: an implementation that sorts nulls correctly and zeros wrong passes either test alone.
/// </para>
/// <para>
/// The third is the sort reaching only what is on screen. There is no pager here, so every row the
/// filter leaves is rendered — which makes this the one explorer where the whole set can be
/// asserted rather than a page of it, and the assertion is on all of it for that reason.
/// </para>
/// </remarks>
public class KildeSortingTest : BunitContext
{
    private static KildeSummary Kilde(
        string name,
        string code,
        int variables = 0,
        string? established = null,
        string? sourceUpdated = null,
        string kildetype = "sentraltHelseregister") =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Kildetype = kildetype,
            IsActive = true,
            DatasamlingCount = 0,
            TotalVariables = variables,
            AdditionalProperties = Properties(established, sourceUpdated),
        };

    /// <summary>
    /// The curated bag, holding only the keys a test asked for.
    /// </summary>
    /// <remarks>
    /// A key is left out entirely rather than set to null, because that is what the API does for a
    /// field nobody filled in — and "the key is absent" is the state the null-last rule has to
    /// survive.
    /// </remarks>
    private static IReadOnlyDictionary<string, string?> Properties(string? established, string? sourceUpdated)
    {
        var properties = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (established is not null)
        {
            properties["Opprettet"] = established;
        }

        if (sourceUpdated is not null)
        {
            properties["SistOppdatert"] = sourceUpdated;
        }

        return properties;
    }

    private sealed class FakeClient(params KildeSummary[] kilder) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(kilder);
    }

    private IRenderedComponent<KildeSearch> RenderWith(params KildeSummary[] kilder)
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(kilder));

        return Render<KildeSearch>();
    }

    private static IReadOnlyList<string> RowNames(IRenderedComponent<KildeSearch> cut) =>
        [.. cut.FindAll(".munin-explorer-kilder tbody th button").Select(b => b.TextContent.Trim())];

    private static void Choose(IRenderedComponent<KildeSearch> cut, KildeSortOrder order) =>
        cut.Find("select[id^='munin-explorer-sort']").Change(order.ToString());

    [Fact]
    public void Order_WhenNobodyHasChosenOne_ThenTheListIsInTheOrderTheCatalogueSentIt()
    {
        // The default has to be the order that arrived and not a name sort, however nearly the two
        // agree: every link made before this control existed opens on it, and the API's own order
        // is a curated fact about the catalogue rather than something to be second-guessed.
        var cut = RenderWith(
            Kilde("Reseptregisteret", "K_NORPD"),
            Kilde("Als registeret", "K_ALS"),
            Kilde("Dødsårsaksregisteret", "K_DAR"));

        Assert.Equal(["Reseptregisteret", "Als registeret", "Dødsårsaksregisteret"], RowNames(cut));
    }

    [Fact]
    public void Order_WhenSortedByName_ThenNorwegianCollationPutsAaAndTheLastThreeLettersLast()
    {
        // The dpc6h regression, on the four spellings that tell the two collations apart, and under
        // an unpinned collator the sequence is whatever the machine is set to. Bbb before Aaa is
        // deliberate: nb collates the digraph aa as å, so a name spelled Aa ends a Norwegian list.
        var cut = RenderWith(
            Kilde("Åpen kilde", "K_AAP"),
            Kilde("Bbb-registeret", "K_BBB"),
            Kilde("Østfold-registeret", "K_OST"),
            Kilde("Aaa-registeret", "K_AAA"),
            Kilde("Ærlig register", "K_AER"));

        Choose(cut, KildeSortOrder.Name);

        Assert.Equal(
            ["Bbb-registeret", "Ærlig register", "Østfold-registeret", "Aaa-registeret", "Åpen kilde"],
            RowNames(cut));
    }

    [Fact]
    public void Order_WhenAKildeHasNoName_ThenItSortsUnderTheCodeTheRowActuallyShows()
    {
        // The row draws the code where the catalogue left the name empty (Fhi.Metadata-w13lk), so
        // sorting the raw name would file that row first while the screen shows it under K_ZZZ.
        var cut = RenderWith(
            Kilde("Als registeret", "K_ALS"),
            Kilde("", "K_ZZZ"),
            Kilde("Barnediabetes", "K_BDR"));

        Choose(cut, KildeSortOrder.Name);

        Assert.Equal(["Als registeret", "Barnediabetes", "K_ZZZ"], RowNames(cut));
    }

    [Fact]
    public void Order_WhenSortedByVariableCount_ThenTheMostComeFirstAndZeroIsAValueAndNotAnAbsence()
    {
        // TotalVariables is a non-nullable int, so the contract cannot say "not counted" — an
        // absent count arrives as 0 (Fhi.Metadata-kbfqs). What this pins is that 0 is ordered as 0,
        // at the bottom of the list, rather than being treated as missing and moved elsewhere.
        var cut = RenderWith(
            Kilde("Tomt register", "K_TOM", variables: 0),
            Kilde("Stort register", "K_STO", variables: 240),
            Kilde("Lite register", "K_LIT", variables: 7));

        Choose(cut, KildeSortOrder.Variables);

        Assert.Equal(["Stort register", "Lite register", "Tomt register"], RowNames(cut));
    }

    [Fact]
    public void Order_WhenSortedByEstablished_ThenARecordedZeroIsAValueAndAnAbsentYearIsLast()
    {
        // The whole of the null/zero rule in one list. The catalogue really does hold "0" as an
        // established year, and it is a value: it sorts below every real year and ABOVE the kilde
        // that has no year at all, which is what keeps "not recorded" from reading as "oldest".
        var cut = RenderWith(
            Kilde("Uten årstall", "K_UTN"),
            Kilde("Nyest", "K_NYE", established: "2023"),
            Kilde("Nullåret", "K_NUL", established: "0"),
            Kilde("Eldst", "K_ELD", established: "1900"));

        Choose(cut, KildeSortOrder.Established);

        Assert.Equal(["Nyest", "Eldst", "Nullåret", "Uten årstall"], RowNames(cut));
    }

    [Fact]
    public void Order_WhenSortedBySourceUpdated_ThenAnUnparsableDateLandsWithTheMissingOnesAndNotFirst()
    {
        // The column renders an unparsable SistOppdatert raw rather than hiding it, so the value is
        // on screen — but it has no place on a timeline, and putting it first or last among the
        // dates would be this package inventing one for it.
        var cut = RenderWith(
            Kilde("Sprøytet dato", "K_RAR", sourceUpdated: "ikke en dato"),
            Kilde("Endret i fjor", "K_GML", sourceUpdated: "20250101"),
            Kilde("Aldri endret", "K_ALD"),
            Kilde("Endret sist", "K_NYT", sourceUpdated: "20260423"));

        Choose(cut, KildeSortOrder.SourceUpdated);

        // The two that have no orderable date come last, between themselves in name order — the
        // tiebreak, not the order they arrived in.
        Assert.Equal(
            ["Endret sist", "Endret i fjor", "Aldri endret", "Sprøytet dato"],
            RowNames(cut));
    }

    [Fact]
    public void Order_WhenTwoKilderShareAValue_ThenTheNameBreaksTheTieRatherThanTheRenderer()
    {
        // Without a tiebreak, OrderByDescending is stable and equal rows keep the order they
        // arrived in — which is not wrong so much as unstated, and it changes with the API's own
        // ordering rather than with anything the reader did.
        var cut = RenderWith(
            Kilde("Ørret-registeret", "K_ORR", variables: 42),
            Kilde("Als registeret", "K_ALS", variables: 42),
            Kilde("Dødsårsaksregisteret", "K_DAR", variables: 42));

        Choose(cut, KildeSortOrder.Variables);

        Assert.Equal(["Als registeret", "Dødsårsaksregisteret", "Ørret-registeret"], RowNames(cut));
    }

    [Fact]
    public void Order_WhenTheListIsNarrowedFirst_ThenEverySurvivingRowIsSortedAndNotJustTheFirstFew()
    {
        // AC1, and the reason it is worth a test of its own: a sort applied before the filter, or
        // to a rendered subset, would look right at the top of the list and be wrong further down.
        // Twenty-six rows, filtered to thirteen, asserted whole.
        var kilder = Enumerable.Range(0, 26)
            .Select(i => Kilde($"Kilde {(char)('A' + i)}", $"K_{(char)('A' + i)}",
                               variables: i,
                               kildetype: i % 2 == 0 ? "biobank" : "sentraltHelseregister"))
            .ToArray();

        var cut = RenderWith(kilder);

        cut.Find(".munin-explorer-filters__toggle").Click();
        cut.FindAll(".munin-explorer-filters__facets input[type=checkbox]")
           .First(box => box.ParentElement!.TextContent.Contains("Biobank", StringComparison.Ordinal))
           .Change(true);

        Choose(cut, KildeSortOrder.Variables);

        // The even indices, most variables first: Y (24), W (22) … A (0).
        var expected = Enumerable.Range(0, 13)
            .Select(i => $"Kilde {(char)('A' + 24 - (i * 2))}")
            .ToArray();

        Assert.Equal(expected, RowNames(cut));
    }

    [Fact]
    public void Order_WhenTheReaderSorts_ThenTheStatusLineSaysWhichOrderTheRowsAreIn()
    {
        // The rows move under a screen reader with nothing announcing it otherwise: the status line
        // is polite and atomic, so a sentence that does not change is a change nobody hears.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"), Kilde("Barnediabetes", "K_BDR"));

        Assert.Equal("2 kilder", cut.Find("p[role=status]").TextContent.Trim());

        Choose(cut, KildeSortOrder.Variables);

        Assert.Equal("2 kilder, sortert etter Flest variabler", cut.Find("p[role=status]").TextContent.Trim());
    }

    [Fact]
    public void Order_WhenTheReaderReadsInEnglish_ThenTheControlAndTheSentenceAreInEnglishToo()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var cut = Render<KildeSearch>(b => b.Add(c => c.Language, "en"));

        Choose(cut, KildeSortOrder.Name);

        Assert.Contains("Name A–Z", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("1 source, sorted by Name A–Z", cut.Find("p[role=status]").TextContent.Trim());
    }

    [Fact]
    public void Control_WhenTheListIsOnScreen_ThenItOffersEveryOrderNamedByItsOwnLabel()
    {
        // Built from Enum.GetValues, so an order added to KildeSortOrder appears here without a
        // second list being edited — and this is what says the labels are the reader's words and
        // not the member names.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"));

        var offered = cut.FindAll("select[id^='munin-explorer-sort'] option")
                         .Select(option => option.TextContent.Trim())
                         .ToArray();

        Assert.Equal(
            ["Standard", "Navn A–Å", "Flest variabler", "Sist endret (nyest først)", "Opprettet (nyest først)"],
            offered);
    }

    [Fact]
    public void Control_WhenTheHostNamesAnOrder_ThenItIsTheOneSelectedOnTheFirstPaint()
    {
        // `selected` on the option rather than `value` on the select: the second is a DOM property
        // the interactive renderer sets, so a statically rendered first paint would show Standard
        // however the reader arrived — and they arrive here from a link.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var cut = Render<KildeSearch>(b => b.Add(c => c.Order, KildeSortOrder.Established));

        Assert.Equal(
            "Opprettet (nyest først)",
            cut.FindAll("select[id^='munin-explorer-sort'] option")
               .Single(o => o.HasAttribute("selected"))
               .TextContent.Trim());
    }

    [Fact]
    public void Control_WhenTheReaderChoosesAnOrder_ThenTheHostIsToldSoItCanPutItInItsUrl()
    {
        var chosen = new List<KildeSortOrder>();

        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var cut = Render<KildeSearch>(b => b.Add(c => c.OrderChanged, order => chosen.Add(order)));

        Choose(cut, KildeSortOrder.Variables);
        Choose(cut, KildeSortOrder.Standard);

        Assert.Equal([KildeSortOrder.Variables, KildeSortOrder.Standard], chosen);
    }

    [Fact]
    public void Control_WhenTheBrowserSendsSomethingUnreadable_ThenTheCatalogueOrderComesBack()
    {
        // A select can only send back a value this component wrote into it, so this is defensive —
        // and it is a fallback rather than a throw for the reason the URL parse is one: whatever
        // arrives, the list is still a list.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"), Kilde("Barnediabetes", "K_BDR"));

        Choose(cut, KildeSortOrder.Name);
        cut.Find("select[id^='munin-explorer-sort']").Change("999");

        Assert.Equal("2 kilder", cut.Find("p[role=status]").TextContent.Trim());
    }

    [Fact]
    public void Control_WhenTheSearchMatchesNothing_ThenNoOrderControlIsDrawnOverTheEmptyTable()
    {
        // The same rule the column picker follows: a control acting on a table that is not there
        // offers nothing, and both are inside the branch that draws the results.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"));

        cut.Find(".searchbox__freetext").Change("finnes ikke");

        Assert.Empty(cut.FindAll("select[id^='munin-explorer-sort']"));
    }

    [Fact]
    public void Control_WhenItIsRead_ThenTheSelectIsNamedByAVisibleLabel()
    {
        // A select with no <label for> is announced as "combo box" and nothing else. AccessibleName
        // refuses placeholder and title on purpose, so this is the name a screen reader really has.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"));

        Assert.Equal("Sorter etter", AccessibleName.Of(cut.Find("select[id^='munin-explorer-sort']")));
    }
}
