using AngleSharp.Dom;
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

    /// <summary>Sort on an order's column, which is the only way a reader can.</summary>
    private static void Choose(IRenderedComponent<KildeSearch> cut, KildeSortOrder order) =>
        KildeColumns.SortBy(cut, order);

    /// <summary>The header cell of the column the list is sorted on, or null when none is.</summary>
    /// <remarks>
    /// Read off <c>aria-sort</c> rather than off the arrow, because the attribute is the half a
    /// screen reader has — and asserted as a single element, which is what says no second column
    /// claims to be sorted too.
    /// </remarks>
    private static IElement? SortedHeader(IRenderedComponent<KildeSearch> cut) =>
        cut.FindAll(".munin-explorer-kilder thead th[aria-sort]").SingleOrDefault();

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
    public void Order_WhenSortedByVariableCount_ThenZeroIsOrderedAsAValueInBothDirections()
    {
        // TotalVariables is a non-nullable int, so the contract cannot say "not counted" — an
        // absent count arrives as 0 (Fhi.Metadata-kbfqs). What this pins is that 0 is ordered as 0,
        // at whichever end of the list the direction puts it, rather than being treated as missing
        // and moved elsewhere.
        var cut = RenderWith(
            Kilde("Tomt register", "K_TOM", variables: 0),
            Kilde("Stort register", "K_STO", variables: 240),
            Kilde("Lite register", "K_LIT", variables: 7));

        // The first press on a column is ascending, the variable explorer's rule.
        Choose(cut, KildeSortOrder.Variables);

        Assert.Equal(["Tomt register", "Lite register", "Stort register"], RowNames(cut));

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

        Assert.Equal(["Nullåret", "Eldst", "Nyest", "Uten årstall"], RowNames(cut));

        // Reversed, and the kilde with no year does not move: "not recorded" is last in both
        // directions, which is the one place a reversed comparison would have put it first.
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

        // Sist endret is one of the seven columns behind the picker, so it has to be on screen
        // before it can be pressed — the price of the order living on the heading.
        KildeColumns.ToggleColumn(cut, "Sist endret");
        Choose(cut, KildeSortOrder.SourceUpdated);

        // The two that have no orderable date come last, between themselves in name order — the
        // tiebreak, not the order they arrived in.
        Assert.Equal(
            ["Endret i fjor", "Endret sist", "Aldri endret", "Sprøytet dato"],
            RowNames(cut));

        Choose(cut, KildeSortOrder.SourceUpdated);

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

        // The even indices, fewest variables first: A (0), C (2) … Y (24).
        var expected = Enumerable.Range(0, 13)
            .Select(i => $"Kilde {(char)('A' + (i * 2))}")
            .ToArray();

        Assert.Equal(expected, RowNames(cut));
    }

    [Fact]
    public void Order_WhenTheReaderHasSearchedFirst_ThenTheOrderRunsOverEverythingTheSearchLeft()
    {
        // The facet half of AC1 is above; this is the search half, and the two narrow through
        // different code. A sort moved ahead of the search, or into the facet branch alone, gives
        // the same answer everywhere except here — a filter and a non-default order both in force.
        var cut = RenderWith(
            Kilde("Als registeret", "K_ALS", variables: 3),
            Kilde("Barnediabetes", "K_BDR", variables: 900),
            Kilde("Dødsårsaksregisteret", "K_DAR", variables: 12),
            Kilde("Reseptregisteret", "K_NORPD", variables: 40),
            Kilde("Kreftregisteret", "K_KRG", variables: 7));

        cut.Find(".searchbox__freetext").Change("register");

        Choose(cut, KildeSortOrder.Variables);

        // Barnediabetes has the most variables of the five and matches none of the search, so a
        // sort that ran over the catalogue rather than over the matches would end this list.
        Assert.Equal(
            ["Als registeret", "Kreftregisteret", "Dødsårsaksregisteret", "Reseptregisteret"],
            RowNames(cut));
    }

    [Fact]
    public void Order_WhenTheReaderSorts_ThenTheStatusLineSaysWhichOrderTheRowsAreInAndWhichWay()
    {
        // The rows move under a screen reader with nothing announcing it otherwise: the status line
        // is polite and atomic, so a sentence that does not change is a change nobody hears. The
        // direction is in it because a second press on the same column changes nothing else in the
        // sentence, and a reader who cannot see the arrow would hear the same words twice.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"), Kilde("Barnediabetes", "K_BDR"));

        Assert.Equal("2 kilder", cut.Find("p[role=status]").TextContent.Trim());

        Choose(cut, KildeSortOrder.Variables);

        Assert.Equal(
            "2 kilder, sortert etter Variabler, stigende",
            cut.Find("p[role=status]").TextContent.Trim());

        Choose(cut, KildeSortOrder.Variables);

        Assert.Equal(
            "2 kilder, sortert etter Variabler, synkende",
            cut.Find("p[role=status]").TextContent.Trim());
    }

    [Fact]
    public void Order_WhenTheReaderReadsInEnglish_ThenTheHeadingAndTheSentenceAreInEnglishToo()
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var cut = Render<KildeSearch>(b => b.Add(c => c.Language, "en"));

        // By the English word, which is the whole point: the heading is the control now, so a
        // column drawn in one language and pressed by another name would be unreachable.
        KildeColumns.SortBy(cut, "Name");

        Assert.Equal("1 source, sorted by Name, ascending", cut.Find("p[role=status]").TextContent.Trim());
    }

    // ---------------------------------------------------------------------------------
    // The control, which is the column heading itself since Fhi.Metadata-l9l2n.88.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Control_WhenTheListIsOnScreen_ThenTheSortableColumnsAreTheFourThatMapToAnOrder()
    {
        // Navn, Variabler and Opprettet are drawn by default and Sist endret sits behind the
        // picker; the other nine columns read values no order covers, so a button over one of them
        // would offer a sort this component cannot perform.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"));

        Assert.Equal(
            ["Navn", "Variabler", "Opprettet"],
            KildeColumns.SortButtons(cut).Select(button => button.TextContent.Trim()));

        KildeColumns.ToggleColumn(cut, "Sist endret");

        Assert.Equal(
            ["Navn", "Variabler", "Opprettet", "Sist endret"],
            KildeColumns.SortButtons(cut).Select(button => button.TextContent.Trim()));
    }

    [Fact]
    public void Control_WhenTheSorterEtterSelectIsLookedFor_ThenThereIsNoneLeftAnywhere()
    {
        // The dropdown is gone on purpose (Robin, 2026-09-11) and the headings are the whole of the
        // control. A select that came back would be a second place to hold the same state, which is
        // what this change was made to stop.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"), Kilde("Barnediabetes", "K_BDR"));

        Assert.Empty(cut.FindAll("select"));
    }

    [Fact]
    public void Control_WhenAColumnIsSorted_ThenOnlyItsOwnHeaderCellCarriesAriaSort()
    {
        // aria-sort on the CELL and on one cell only. "none" on every other column is noise a
        // screen reader reads out on the way past, and the attribute on the button rather than the
        // cell would describe the control instead of the column.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"), Kilde("Barnediabetes", "K_BDR"));

        Assert.Null(SortedHeader(cut));

        Choose(cut, KildeSortOrder.Name);

        var sorted = SortedHeader(cut);

        Assert.NotNull(sorted);
        Assert.Equal("ascending", sorted!.GetAttribute("aria-sort"));
        Assert.StartsWith("Navn", sorted.TextContent.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain("aria-sort", cut.Find(".munin-explorer-kilder__sort").OuterHtml);

        Choose(cut, KildeSortOrder.Name);

        Assert.Equal("descending", SortedHeader(cut)!.GetAttribute("aria-sort"));

        // Another column takes it whole: two columns claiming to be sorted is the failure this and
        // the SingleOrDefault in SortedHeader are both here for.
        Choose(cut, KildeSortOrder.Established);

        Assert.Equal("ascending", SortedHeader(cut)!.GetAttribute("aria-sort"));
        Assert.StartsWith("Opprettet", SortedHeader(cut)!.TextContent.Trim(), StringComparison.Ordinal);
    }

    [Fact]
    public void Control_WhenAHeadingIsDrawn_ThenItIsAButtonInsideTheCellWearingTheSettledName()
    {
        // type="button" or the press submits a host's surrounding form; the class is the one
        // Fhi.Helsedata.Stiler's rule names (Fhi.Metadata-l9l2n.106) and cannot be renamed here
        // alone. A real <button> is also what gives Enter, Space and Tab without a key handler.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"));

        var button = KildeColumns.SortButtons(cut)[0];

        Assert.Equal("th", button.ParentElement!.LocalName);
        Assert.Equal("col", button.ParentElement.GetAttribute("scope"));
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("hd-button-reset munin-explorer-kilder__sort", button.GetAttribute("class"));

        // The cell is a native columnheader, so an explicit role on it would be a redundant one.
        Assert.False(button.ParentElement.HasAttribute("role"));
    }

    [Fact]
    public void Control_WhenAColumnIsSorted_ThenTheArrowIsAClasslessSpanNoScreenReaderAnnounces()
    {
        // Drawn exactly as the variable explorer draws it: the character is the whole of the mark,
        // it needs no CSS, and it is hidden because aria-sort above already says the same thing.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"));

        Choose(cut, KildeSortOrder.Name);

        var arrow = cut.Find(".munin-explorer-kilder thead th[aria-sort] span");

        Assert.Equal("true", arrow.GetAttribute("aria-hidden"));
        Assert.False(arrow.HasAttribute("class"));
        Assert.Equal("↑", arrow.TextContent.Trim());

        Choose(cut, KildeSortOrder.Name);

        Assert.Equal("↓", cut.Find(".munin-explorer-kilder thead th[aria-sort] span").TextContent.Trim());

        // And on that column alone: an arrow left behind on the column the reader sorted on before
        // would say the list is in two orders at once.
        Assert.Single(cut.FindAll(".munin-explorer-kilder thead span[aria-hidden]"));
    }

    [Fact]
    public void Control_WhenAHeadingIsRead_ThenItIsNamedByTheColumnRatherThanByTheOrdering()
    {
        // The button says what the COLUMN is: the ordering is the arrow's and aria-sort's to carry,
        // and a button reading "Navn A–Å" would name an order rather than the thing under it —
        // which is what the labels said while a select offered them.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"));

        Assert.Equal("Navn", AccessibleName.Of(KildeColumns.SortButtons(cut)[0]));

        Choose(cut, KildeSortOrder.Name);

        // Still "Navn" once it is the sorted column: the arrow is aria-hidden, so it is not part of
        // the name a screen reader announces.
        Assert.Equal("Navn", AccessibleName.Of(KildeColumns.SortButtons(cut)[0]));
    }

    [Fact]
    public void Control_WhenTheHostNamesAnOrder_ThenTheListArrivesInItOnTheFirstPaint()
    {
        // The host's parameter is where a link's order arrives, and the heading has to show it
        // without a press: a component that only marked the column it had been pressed on would
        // open every shared link looking unsorted.
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(
            Kilde("Als registeret", "K_ALS", established: "1990"),
            Kilde("Barnediabetes", "K_BDR", established: "2020")));

        var cut = Render<KildeSearch>(b => b
            .Add(c => c.Order, KildeSortOrder.Established)
            .Add(c => c.Direction, SortDirection.Descending));

        Assert.Equal("descending", SortedHeader(cut)!.GetAttribute("aria-sort"));
        Assert.StartsWith("Opprettet", SortedHeader(cut)!.TextContent.Trim(), StringComparison.Ordinal);
        Assert.Equal(["Barnediabetes", "Als registeret"], RowNames(cut));
    }

    [Fact]
    public void Control_WhenTheReaderSorts_ThenTheHostIsToldBothHalvesSoItCanPutThemInItsUrl()
    {
        // Both callbacks on every press, including the one that only reverses: a host told the
        // order and not the direction would write a link that opens the same column the other way.
        var orders = new List<KildeSortOrder>();
        var directions = new List<SortDirection>();

        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilde("Als registeret", "K_ALS")));

        var cut = Render<KildeSearch>(b => b
            .Add(c => c.OrderChanged, order => orders.Add(order))
            .Add(c => c.DirectionChanged, direction => directions.Add(direction)));

        Choose(cut, KildeSortOrder.Variables);
        Choose(cut, KildeSortOrder.Variables);
        Choose(cut, KildeSortOrder.Name);

        Assert.Equal(
            [KildeSortOrder.Variables, KildeSortOrder.Variables, KildeSortOrder.Name],
            orders);

        Assert.Equal(
            [SortDirection.Ascending, SortDirection.Descending, SortDirection.Ascending],
            directions);
    }

    [Fact]
    public void Control_WhenTheSearchMatchesNothing_ThenThereIsNoHeadingToSortByEither()
    {
        // The table goes with the rows, and the headings with the table: there is nothing to order
        // and nothing offering to.
        var cut = RenderWith(Kilde("Als registeret", "K_ALS"));

        cut.Find(".searchbox__freetext").Change("finnes ikke");

        Assert.Empty(KildeColumns.SortButtons(cut));
    }
}
