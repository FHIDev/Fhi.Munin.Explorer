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
/// two contract fields. The expected string is computed from the helper rather than written out,
/// so an ICU release that renames a month moves all four together. (Fhi.Metadata-ufmop)
/// </remarks>
public class DataPeriodAcrossSurfacesTest : ExplorerTestContext
{
    private static readonly Guid VariableId = new("aaaaaaaa-0000-0000-0000-000000000001");

    private static readonly Guid ListId = new("bbbbbbbb-0000-0000-0000-000000000002");

    private static readonly DateTimeOffset From = new(1979, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Deliberately not the first of a month: 212 of 1000 variables sampled off the live catalogue
    // carry a day `MMM yyyy` would have discarded, which is why this surface shows days at all.
    private static readonly DateTimeOffset To = new(2024, 12, 31, 0, 0, 0, TimeSpan.Zero);

    private static VariableSummary Row() => new()
    {
        Id = VariableId,
        Code = "ABORT_UTLEVERING",
        PreferredTerm = "Utlevering",
        KildeName = "Abortregisteret",
        DataFrom = From,
        DataTo = To,
    };

    private static VariableDetail Whole() => new()
    {
        Id = VariableId,
        Code = "ABORT_UTLEVERING",
        PreferredTerm = "Utlevering",
        KildeName = "Abortregisteret",
        DataFrom = From,
        DataTo = To,
    };

    private static VariableListItem Saved() => new()
    {
        VariableId = VariableId,
        AddedAt = DateTimeOffset.UtcNow,
        VariableCode = "ABORT_UTLEVERING",
        VariableName = "Utlevering",
        KildeName = "Abortregisteret",
        DataFrom = From,
        DataTo = To,
    };

    private sealed class AbortregisteretClient : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [Row()],
                TotalCount = 1,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(Whole());

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = ListId, Name = "Mine variabler", VariableCount = 1 }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [Saved()],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("en")]
    public void DataPeriod_WhenOneVariableIsDrawnOnFourSurfaces_ThenAllFourReadTheSame(string? language)
    {
        var expected = $"{CatalogueDate.Day(From, language, DateWidth.Narrow)} – " +
                       $"{CatalogueDate.Day(To, language, DateWidth.Narrow)}";

        IMuninExplorerClient client = new AbortregisteretClient();

        Services.AddSingleton(client);
        Services.AddScoped<VariableListState>();

        var search = Render<VariableSearch>(b => b.Add(c => c.Language, language));

        search.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click();

        var page = Render<VariableView>(b => b
            .Add(c => c.Variable, Whole())
            .Add(c => c.Language, language));

        var list = Render<VariableListView>(b => b
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, language ?? "no"));

        var cell = search.Find(
            ".munin-explorer-dataitem-main__period .munin-explorer-dataitem-main__column__text");
        var bar = search.Find(".munin-explorer-period__range");
        var block = page.Find($"#{DetailSectionIds.DataPeriod} p");
        var saved = list.Find(
            "td.munin-explorer-dataitem-main__period .munin-explorer-dataitem-main__column__text");

        Assert.Equal(expected, cell.TextContent.Trim());
        Assert.Equal(expected, bar.TextContent.Trim());
        Assert.Equal(expected, block.TextContent.Trim());
        Assert.Equal(expected, saved.TextContent.Trim());
    }
}
