using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// Held-out regression for avansert søk (Fhi.Metadata-ipok2.2): a half-typed native date in a row must be refused, not dropped.
public class AdvancedSearchDateHeldOutTest : ExplorerTestContext
{
    private static readonly IReadOnlyList<ConditionField> Fields =
        JsonSerializer.Deserialize<IReadOnlyList<ConditionField>>(TestData.Read("vilkar.json"), MuninExplorerClient.Json)!;

    private sealed class ConditionClient : EmptyMuninExplorerClient
    {
        public List<VariableFilter?> Searches { get; } = [];

        public override Task<IReadOnlyList<ConditionField>> GetConditionFieldsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Fields);

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(filter);
            return Task.FromResult(new Page<VariableSummary>
            {
                Items = [new VariableSummary { Id = Guid.NewGuid(), Code = "V_A", PreferredTerm = "Blodtrykk" }],
                TotalCount = 1,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1
            });
        }
    }

    private IRenderedComponent<VariableSearch> OpenWithDateRow(ConditionClient client, DateInputState browser)
    {
        JSInterop.SetupModule(ExplorerInterop.ModulePath)
            .Setup<DateInputState>("readDateInput", _ => true)
            .SetResult(browser);
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddSingleton(new AdvancedSearchSwitch(true));
        var cut = Render<VariableSearch>();
        IElement? toggle = null;
        cut.WaitForAssertion(() =>
        {
            toggle = cut.FindAll("button[aria-expanded]").FirstOrDefault(b => b.TextContent.StartsWith("Avansert søk", StringComparison.Ordinal));
            Assert.NotNull(toggle);
        });
        toggle!.Click();
        Row(cut).QuerySelectorAll("select")[0].Change("gyldigFra");
        Row(cut).QuerySelectorAll("select")[1].Change("for");
        return cut;
    }

    private static IElement Row(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("fieldset fieldset").Single(f => f.QuerySelector("legend")?.TextContent.StartsWith("Betingelse", StringComparison.Ordinal) == true);

    private static void ShowHits(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Vis treff").Click();

    [Fact]
    public void ShowHits_WhenADateRowHoldsABadNativeEntry_ThenTheRowIsRefusedAndTheFormStaysOpen()
    {
        var client = new ConditionClient();
        var cut = OpenWithDateRow(client, new DateInputState("", true));
        var before = client.Searches.Count;

        ShowHits(cut);

        Assert.Equal(before, client.Searches.Count);
        var toggle = cut.FindAll("button[aria-expanded]").First(b => b.TextContent.StartsWith("Avansert søk", StringComparison.Ordinal));
        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        Assert.NotNull(Row(cut).QuerySelector("[role=alert]"));
        Assert.NotEmpty(Row(cut).QuerySelector("[role=alert]")!.TextContent.Trim());
        Assert.Equal("true", Row(cut).QuerySelector("input[type=date]")!.GetAttribute("aria-invalid"));
    }

    [Fact]
    public void ShowHits_WhenADateRowIsReallyCleared_ThenItIsJustEmptyAndTheFormCloses()
    {
        var client = new ConditionClient();
        var cut = OpenWithDateRow(client, new DateInputState("", false));

        ShowHits(cut);

        cut.WaitForAssertion(() => Assert.Equal("false",
            cut.FindAll("button[aria-expanded]").First(b => b.TextContent.StartsWith("Avansert søk", StringComparison.Ordinal)).GetAttribute("aria-expanded")));
    }
}
