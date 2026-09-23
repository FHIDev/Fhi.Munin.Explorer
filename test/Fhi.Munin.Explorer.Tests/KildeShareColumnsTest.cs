using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static Fhi.Munin.Explorer.Tests.KildeColumns;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The kilde table's two coverage columns, Kodeverk % and Statistikk % (Fhi.Metadata-l9l2n.98).
/// </summary>
/// <remarks>
/// The trap is null against zero. Null is a kilde with no variables to measure and 0 is a measured
/// none, and a column that draws them alike tells the reader either "we do not know" about a source
/// that publishes no statistics, or "publishes none" about one that publishes nothing at all. So
/// the two are asserted to differ in markup, not merely both to have text.
/// </remarks>
public class KildeShareColumnsTest : ExplorerTestContext
{
    private const string KodeverkHeading = "Kodeverk %";
    private const string StatisticsHeading = "Statistikk %";

    private static KildeSummary Kilde(string name, string code, int? kodeverk, int? statistics) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Kildetype = "sentraltHelseregister",
            IsActive = true,
            DatasamlingCount = 1,
            TotalVariables = kodeverk is null ? 0 : 10,
            KodeverkShare = kodeverk,
            StatisticsShare = statistics,
        };

    private static readonly KildeSummary[] Kilder =
    [
        Kilde("Aregisteret", "K_A", kodeverk: 37, statistics: 95),
        Kilde("Bregisteret", "K_B", kodeverk: 0, statistics: 0),
        Kilde("Cregisteret", "K_C", kodeverk: null, statistics: null),
    ];

    private sealed class FakeClient(params KildeSummary[] kilder) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(kilder);
    }

    private IRenderedComponent<KildeSearch> RenderWith(string language = "no")
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Kilder));

        return Render<KildeSearch>(b => b.Add(c => c.Language, language));
    }

    private static void TurnSharesOn(IRenderedComponent<KildeSearch> cut, string language = "no")
    {
        var t = Texts.For(language);
        ToggleColumn(cut, t.ColumnKodeverkShare);
        ToggleColumn(cut, t.ColumnStatisticsShare);
    }

    /// <summary>The cell under <paramref name="heading"/> in the row of the kilde with this name.</summary>
    private static IElement Cell(IRenderedComponent<KildeSearch> cut, string name, string heading)
    {
        var column = Headers(cut).ToList().IndexOf(heading);
        Assert.True(column >= 0, $"No {heading} heading in the table.");

        return cut.FindAll(".munin-explorer-kilder tbody tr")
            .Single(tr => tr.QuerySelector("th button")?.TextContent.Trim() == name)
            .QuerySelectorAll("th, td")[column];
    }

    private static string Normalised(IElement cell) => cell.TextContent.Replace('\u00A0', ' ').Trim();

    [Fact]
    public void ColumnKeys_Always_ThenTheSharesAreOptionalAndNotInTheDefaultSet()
    {
        Assert.Contains("andelKodeverk", KildeSearch.ColumnKeys);
        Assert.Contains("andelStatistikk", KildeSearch.ColumnKeys);
        Assert.DoesNotContain("andelKodeverk", KildeSearch.DefaultColumnKeys);
        Assert.DoesNotContain("andelStatistikk", KildeSearch.DefaultColumnKeys);
    }

    [Fact]
    public void Render_WhenTheListLoads_ThenNeitherShareColumnIsDrawn()
    {
        var cut = RenderWith();

        Assert.DoesNotContain(KodeverkHeading, Headers(cut));
        Assert.DoesNotContain(StatisticsHeading, Headers(cut));
        Assert.DoesNotContain(cut.FindAll(".munin-explorer-kilder tbody .screenreader-only"),
            e => e.TextContent == "ikke målt");
    }

    [Fact]
    public void Picker_WhenBothSharesAreTurnedOn_ThenEachHasAColumnHeaderAndACellPerRow()
    {
        var cut = RenderWith();

        TurnSharesOn(cut);

        var headers = cut.FindAll(".munin-explorer-kilder thead th").ToList();
        foreach (var heading in new[] { KodeverkHeading, StatisticsHeading })
        {
            var th = Assert.Single(headers, h => h.TextContent.Trim() == heading);
            Assert.Equal("col", th.GetAttribute("scope"));
        }

        foreach (var row in cut.FindAll(".munin-explorer-kilder tbody tr"))
        {
            Assert.Equal(headers.Count, row.QuerySelectorAll("th, td").Length);
        }
    }

    [Fact]
    public void Cells_WhenOneShareIsNullAndAnotherZero_ThenTheyRenderDifferentMarkup()
    {
        // Both cells have text, which is why that alone proves nothing: the null one must be the
        // dash with words for a screen reader, the zero one a measured "0 %".
        var cut = RenderWith();
        TurnSharesOn(cut);

        var zero = Cell(cut, "Bregisteret", KodeverkHeading);
        var unmeasured = Cell(cut, "Cregisteret", KodeverkHeading);

        Assert.NotEqual(zero.InnerHtml, unmeasured.InnerHtml);

        Assert.Equal("0 %", Normalised(zero));
        Assert.DoesNotContain("–", zero.TextContent, StringComparison.Ordinal);
        Assert.Null(zero.QuerySelector("[aria-hidden]"));

        var glyph = Assert.Single(unmeasured.QuerySelectorAll("[aria-hidden=true]"));
        Assert.Equal("–", glyph.TextContent);
        Assert.Equal("ikke målt", unmeasured.QuerySelector(".screenreader-only")?.TextContent);
        Assert.DoesNotContain("0", unmeasured.TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("no", "ikke målt")]
    [InlineData("en", "not measured")]
    public void Dash_Always_ThenItsAccessibleTextIsWordsInTheReadersLanguage(string language, string expected)
    {
        var cut = RenderWith(language);
        TurnSharesOn(cut, language);

        foreach (var heading in new[] { Texts.For(language).ColumnKodeverkShare, Texts.For(language).ColumnStatisticsShare })
        {
            var cell = Cell(cut, "Cregisteret", heading);

            // What a screen reader reads is the cell's text minus what is aria-hidden: the words.
            var spoken = string.Concat(cell.ChildNodes
                .Where(n => n is not IElement e || e.GetAttribute("aria-hidden") != "true")
                .Select(n => n.TextContent)).Trim();
            Assert.Equal(expected, spoken);
        }
    }

    [Theory]
    [InlineData("no")]
    [InlineData("en")]
    public void Headings_Always_ThenTheyAreTheSameCatalogueWordsInBothLanguages(string language)
    {
        Assert.Equal(KodeverkHeading, Texts.For(language).ColumnKodeverkShare);
        Assert.Equal(StatisticsHeading, Texts.For(language).ColumnStatisticsShare);
    }

    [Fact]
    public void Cells_WhenTheApiSendsAPercent_ThenItIsShownAsSentWithoutRounding()
    {
        var cut = RenderWith();
        TurnSharesOn(cut);

        Assert.Equal("37 %", Normalised(Cell(cut, "Aregisteret", KodeverkHeading)));
        Assert.Equal("95 %", Normalised(Cell(cut, "Aregisteret", StatisticsHeading)));
    }

    [Fact]
    public void Cells_Always_ThenTheyAreCountCellsAndOnlyNullAndZeroAreDimmed()
    {
        var cut = RenderWith();
        TurnSharesOn(cut);

        string[] count = ["munin-explorer-kilder__count"];
        string[] dimmed = ["munin-explorer-kilder__count", "munin-explorer-kilder__count--zero"];

        foreach (var heading in new[] { KodeverkHeading, StatisticsHeading })
        {
            Assert.Equal(count, Cell(cut, "Aregisteret", heading).ClassList.ToArray());
            Assert.Equal(dimmed, Cell(cut, "Bregisteret", heading).ClassList.ToArray());
            Assert.Equal(dimmed, Cell(cut, "Cregisteret", heading).ClassList.ToArray());
        }

        foreach (var th in cut.FindAll(".munin-explorer-kilder thead th")
                     .Where(h => h.TextContent.Trim() is KodeverkHeading or StatisticsHeading))
        {
            Assert.Equal(count, th.ClassList.ToArray());
        }
    }

    [Fact]
    public void ScrollBox_WhenEveryOptionalColumnIsOn_ThenTheModifierCountsThirteenOptionalColumns()
    {
        var cut = RenderWith();

        TurnEveryColumnOn(cut);

        // Not selectable here, so four base columns: 4 + 13. The selectable 18 is KildeSelectionTest's.
        Assert.Contains(
            "munin-explorer-kilder-scroll--cols-17",
            cut.Find(".munin-explorer-kilder-scroll").ClassList);
    }

    [Fact]
    public void Headers_WhenTheSharesAreOn_ThenNeitherIsSortable()
    {
        var cut = RenderWith();
        TurnSharesOn(cut);

        foreach (var th in cut.FindAll(".munin-explorer-kilder thead th")
                     .Where(h => h.TextContent.Trim() is KodeverkHeading or StatisticsHeading))
        {
            Assert.Null(th.GetAttribute("aria-sort"));
            Assert.Null(th.QuerySelector("button"));
        }
    }

    [Fact]
    public void Search_WhenTheTermIsOnlyAShareValue_ThenNoKildeMatchesOnIt()
    {
        // Display-only: a share is not something free-text search reads, with the column on or off.
        var cut = RenderWith();
        TurnSharesOn(cut);

        cut.Find(".searchbox__freetext").Change("37");
        Assert.Empty(cut.FindAll(".munin-explorer-kilder tbody th button"));

        cut.Find(".searchbox__freetext").Change("Aregisteret");
        Assert.Equal(
            ["Aregisteret"],
            cut.FindAll(".munin-explorer-kilder tbody th button").Select(b => b.TextContent.Trim()));
    }
}
