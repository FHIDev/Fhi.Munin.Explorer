using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// One variable's dataperiode, read off all four surfaces that draw it.
/// </summary>
/// <remarks>
/// Cross-surface on purpose. Each surface's own tests can be green while the four disagree, which
/// is what a helsedata tester reported against Abortregisteret Utlevering: a result row saying
/// "jan 1979 – des 2024" over a variable page saying "1. jan. 1979 – 31. des. 2024", from the same
/// two contract fields. The format was settled first (Fhi.Metadata-ufmop), the join after it: an
/// unknown start reads "?", and neither way of carrying no date draws year 1 (Fhi.Metadata-msax9).
/// </remarks>
public class DataPeriodAcrossSurfacesTest : ExplorerTestContext
{
    // No run of "0001" in either: the last assertion below reads the whole markup for one, and a
    // fixture id carrying the year 1 by coincidence would excuse the defect it is there to catch.
    private static readonly Guid VariableId = new("aaaaaaaa-1111-2222-3333-444444444444");

    private static readonly Guid ListId = new("bbbbbbbb-5555-6666-7777-888888888888");

    private const string Start = "1979-01-01";

    // Deliberately not the first of a month: 212 of 1000 variables sampled off the live catalogue
    // carry a day `MMM yyyy` would have discarded, which is why this surface shows days at all.
    private const string End = "2024-12-31";

    /// <summary>The other way a payload carries no date: <c>default(DateTimeOffset)</c>.</summary>
    /// <remarks>
    /// A theory row cannot hold one, so the two absences are spelled null and <c>Unset</c> here and
    /// turned back into a date by <see cref="Given"/>. Both have to be rows: a surface reading only
    /// the null one draws 1. jan. 0001 for the other, which is what three of these four did.
    /// </remarks>
    private const string Unset = "";

    /// <summary>The value a payload carries, which is not always a date it knows.</summary>
    private static DateTimeOffset? Given(string? spec) => spec switch
    {
        null => null,
        Unset => default(DateTimeOffset),
        _ => new DateTimeOffset(DateTime.ParseExact(spec, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                                TimeSpan.Zero),
    };

    /// <summary>The date a reader is owed, and null for either way of carrying none.</summary>
    private static DateTimeOffset? Known(string? spec) => spec is null or Unset ? null : Given(spec);

    /// <summary>What every surface has to write, or null where the catalogue gave neither end.</summary>
    /// <remarks>
    /// The join is written out here rather than read back off <see cref="CatalogueDate.Period"/>,
    /// which is the decision under test; only the day's format is borrowed, so an ICU release that
    /// renames a month still moves all four surfaces together rather than reddening them.
    /// </remarks>
    private static string? Expected(string? from, string? to, string? language)
    {
        if (Known(from) is null && Known(to) is null)
        {
            return null;
        }

        var start = Known(from) is { } f ? CatalogueDate.Day(f, language, DateWidth.Narrow) : "?";
        var end = Known(to) is { } t
            ? CatalogueDate.Day(t, language, DateWidth.Narrow)
            : language == "en" ? "Ongoing" : "Pågående";

        return $"{start} – {end}";
    }

    private static string NotSpecified(string? language) =>
        language == "en" ? "Not specified" : "Ikke oppgitt";

    /// <summary>The markup a reader is shown, with the ids and the wiring between them taken out.</summary>
    /// <remarks>
    /// Every component here stamps its ids with eight random hex digits, which spell "0001" about
    /// once in thirteen thousand renders — a flake in the one assertion below that reads the whole
    /// page rather than one cell of it. No date is ever written into any of these attributes.
    /// </remarks>
    private static string Shown(string markup) =>
        Regex.Replace(markup, "\\s(id|for|name|href|aria-[a-z]+)=\"[^\"]*\"", " ");

    private static VariableSummary Row(string? from, string? to) => new()
    {
        Id = VariableId,
        Code = "ABORT_UTLEVERING",
        PreferredTerm = "Utlevering",
        KildeName = "Abortregisteret",
        DataFrom = Given(from),
        DataTo = Given(to),
    };

    private static VariableDetail Whole(string? from, string? to) => new()
    {
        Id = VariableId,
        Code = "ABORT_UTLEVERING",
        PreferredTerm = "Utlevering",
        KildeName = "Abortregisteret",
        DataFrom = Given(from),
        DataTo = Given(to),
    };

    private static VariableListItem Saved(string? from, string? to) => new()
    {
        VariableId = VariableId,
        AddedAt = DateTimeOffset.UtcNow,
        VariableCode = "ABORT_UTLEVERING",
        VariableName = "Utlevering",
        KildeName = "Abortregisteret",
        DataFrom = Given(from),
        DataTo = Given(to),
    };

    private sealed class AbortregisteretClient(string? from, string? to) : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [Row(from, to)],
                TotalCount = 1,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(Whole(from, to));

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = ListId, Name = "Mine variabler", VariableCount = 1 }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [Saved(from, to)],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
    }

    // Both ends known: the shape the four first disagreed about the FORMAT of.
    [Theory]
    [InlineData(Start, End, null)]
    [InlineData(Start, End, "en")]
    // An unknown start, carried both ways. Null is what the catalogue sends; the default date is
    // what a substituted client hands over, and three surfaces drew it as 1. jan. 0001.
    [InlineData(null, End, null)]
    [InlineData(null, End, "en")]
    [InlineData(Unset, End, null)]
    [InlineData(Unset, End, "en")]
    // An open end, carried both ways. The null row guards the default one: it passed before this
    // bead, so a surface that regresses on both at once still reddens twice.
    [InlineData(Start, null, null)]
    [InlineData(Start, null, "en")]
    [InlineData(Start, Unset, null)]
    [InlineData(Start, Unset, "en")]
    // Neither end, carried both ways, with the null row guarding the default one as above.
    [InlineData(null, null, null)]
    [InlineData(null, null, "en")]
    [InlineData(Unset, Unset, null)]
    [InlineData(Unset, Unset, "en")]
    public void DataPeriod_WhenOneVariableIsDrawnOnFourSurfaces_ThenAllFourReadTheSame(
        string? from, string? to, string? language)
    {
        var expected = Expected(from, to, language);

        IMuninExplorerClient client = new AbortregisteretClient(from, to);

        Services.AddSingleton(client);
        Services.AddScoped<VariableListState>();

        var search = Render<VariableSearch>(b => b.Add(c => c.Language, language));

        // The chevron, not the name: the name opens the whole variable in place of the list
        // (Fhi.Metadata-35w0p.34), and the row this reads its period cell off goes with it.
        search.Find(
            "ul.munin-explorer-data-list button.munin-explorer-dataitem__expand-toggle").Click();

        var page = Render<VariableView>(b => b
            .Add(c => c.Variable, Whole(from, to))
            .Add(c => c.Language, language));

        var list = Render<VariableListView>(b => b
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, language ?? "no"));

        var cell = search.Find(
            ".munin-explorer-dataitem-main__period .munin-explorer-dataitem-main__column__text");
        var saved = list.Find(
            "td.munin-explorer-dataitem-main__period .munin-explorer-dataitem-main__column__text");
        var panel = search.FindAll("dl > div")
                          .Single(row => row.QuerySelector("dt")?.TextContent
                                         == (language == "en" ? "Data period" : "Dataperiode"))
                          .QuerySelector("dd")!;
        var blocks = page.FindAll($"#{DetailSectionIds.DataPeriod} p");

        Assert.Equal(expected ?? NotSpecified(language), cell.TextContent.Trim());
        Assert.Equal(expected ?? NotSpecified(language), saved.TextContent.Trim());
        Assert.Equal(expected ?? NotSpecified(language), panel.TextContent.Trim());

        if (expected is null)
        {
            // Nothing to put in it, so the block is not drawn at all — the fact lists write their
            // own "Ikke oppgitt", a whole section cannot.
            Assert.Empty(blocks);
        }
        else
        {
            Assert.Equal(expected, blocks[0].TextContent.Trim());
        }

        // The bar is the panel's illustration of the same two dates, and it is not drawn without
        // them. Its track says which end is open, which is the one thing the words do not.
        var bars = search.FindAll(".munin-explorer-period__range");

        if (expected is null)
        {
            Assert.Empty(bars);
        }
        else
        {
            Assert.Equal(expected, bars[0].TextContent.Trim());
            Assert.Equal(Known(to) is null,
                         search.Find(".munin-explorer-period__track")
                               .ClassList.Contains("munin-explorer-period__track--ongoing"));
        }

        // The year 1 is not a date the catalogue gave, so no surface may print one — in a cell, in
        // a hover title, or anywhere else the assertions above do not reach.
        foreach (var markup in new[] { search.Markup, page.Markup, list.Markup })
        {
            Assert.DoesNotContain("0001", Shown(markup), StringComparison.Ordinal);
        }
    }
}
