using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// One variable's dataperiode, read off every surface that draws it.
/// </summary>
/// <remarks>
/// Cross-surface on purpose. Each surface's own tests can be green while the three disagree, which
/// is what a helsedata tester reported against Abortregisteret Utlevering: a result row saying
/// "jan 1979 – des 2024" over a variable page saying "1. jan. 1979 – 31. des. 2024", from the same
/// two contract fields. (Fhi.Metadata-ufmop)
/// </remarks>
public class DataPeriodAcrossSurfacesTest : ExplorerTestContext
{
    private static readonly Guid VariableId = new("aaaaaaaa-0000-0000-0000-000000000001");

    private static readonly DateTimeOffset From = new(1979, 1, 1, 0, 0, 0, TimeSpan.Zero);

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
    }

    [Theory]
    [InlineData(null, "1. jan. 1979 – 31. des. 2024")]
    [InlineData("en", "1 Jan 1979 – 31 Dec 2024")]
    public void DataPeriod_WhenOneVariableIsDrawnOnThreeSurfaces_ThenAllThreeReadTheSame(
        string? language, string expected)
    {
        IMuninExplorerClient client = new AbortregisteretClient();

        Services.AddSingleton(client);

        var search = Render<VariableSearch>(b => b.Add(c => c.Language, language));

        search.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click();

        var page = Render<VariableView>(b => b
            .Add(c => c.Variable, Whole())
            .Add(c => c.Language, language));

        var cell = search.Find(
            ".munin-explorer-dataitem-main__period .munin-explorer-dataitem-main__column__text");
        var bar = search.Find(".munin-explorer-period__range");
        var block = page.Find($"#{DetailSectionIds.DataPeriod} p");

        Assert.Equal(expected, cell.TextContent.Trim());
        Assert.Equal(expected, bar.TextContent.Trim());
        Assert.Equal(expected, block.TextContent.Trim());
    }
}
