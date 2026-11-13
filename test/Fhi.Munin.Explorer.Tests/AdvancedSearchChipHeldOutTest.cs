using System.Text.Json;
using Bunit;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class AdvancedSearchChipHeldOutTest : ExplorerTestContext
{
    private sealed class Client : EmptyMuninExplorerClient
    {
        public List<VariableFilter?> Searches { get; } = [];

        public override Task<IReadOnlyList<ConditionField>> GetConditionFieldsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonSerializer.Deserialize<IReadOnlyList<ConditionField>>(TestData.Read("vilkar.json"), MuninExplorerClient.Json)!);

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(filter);
            return Task.FromResult(new Page<VariableSummary>
            {
                Items = [new VariableSummary { Id = Guid.NewGuid(), Code = "V_A", PreferredTerm = "Blodtrykk" }],
                TotalCount = 1, PageNumber = 1, Size = 25, TotalPages = 1
            });
        }
    }

    [Fact]
    public async Task Chip_WhenTheSameStaleHandlerRunsTwice_ThenTheSecondRunRemovesNothingMore()
    {
        var client = new Client();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddSingleton(new AdvancedSearchSwitch(true));
        var cut = Render<VariableSearch>(b => b.Add(c => c.Filter, VariableFilter.Parse(
            "vilkar=navn%3Ainneholder%3Ablod&vilkar=kode%3AbegynnerMed%3AV_")));
        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        // The delegate the panel handed chip a is captured now, before any press, and run twice.
        var chips = (IReadOnlyList<ActiveFilters.Chip>)typeof(VariableSearch)
            .GetProperty("ActiveFilterChips", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!;
        var chipA = chips.Single(c => c.Text == "Navn inneholder «blod»").Remove;

        await cut.InvokeAsync(chipA);
        cut.WaitForAssertion(() => Assert.Single(client.Searches[^1]!.Conditions));
        await cut.InvokeAsync(chipA);

        var last = client.Searches[^1]!.Conditions;
        Assert.Equal(["kode:begynnerMed:V_"], last.Select(c => c.ToString()).ToArray());
        Assert.Contains("Kode begynner med «V_»", cut.Markup, StringComparison.Ordinal);
    }
}
