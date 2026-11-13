using System.Text.Json;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

public class AdvancedSearchCodeNameHeldOutTest : ExplorerTestContext
{
    private const string Norwegian = "Åpne data/statistikk tilgjengelig";
    private const string English = "Open data/statistics available";

    private sealed class Client : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<ConditionField>> GetConditionFieldsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonSerializer.Deserialize<IReadOnlyList<ConditionField>>(TestData.Read("vilkar.json"), MuninExplorerClient.Json)!);

        public override Task<IReadOnlyList<PropertyMetadataEntry>> GetKildePropertyMetadataAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PropertyMetadataEntry>>(
            [
                new PropertyMetadataEntry
                {
                    Key = AccessLevels.Key,
                    OptionsJson = $$"""[{"value":"apne-data","label":"{{Norwegian}}","labelEn":"{{English}}"}]""",
                }
            ]);
    }

    private IRenderedComponent<VariableSearch> RenderEnglish()
    {
        Services.AddSingleton<IMuninExplorerClient>(new Client());
        Services.AddSingleton(new AdvancedSearchSwitch(true));
        return Render<VariableSearch>(b => b
            .Add(c => c.Language, "en-GB")
            .Add(c => c.Filter, VariableFilter.Parse("vilkar=tilgangsniva%3AerEnAv%3Aapne-data")));
    }

    private static void WaitForChip(IRenderedComponent<VariableSearch> cut) =>
        cut.WaitForAssertion(() => Assert.Contains("Access level", cut.Markup, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Chip_WhenTheHostMountsWithEnGbAndTheOptionHasAnEnglishLabel_ThenTheCodeReadsInEnglish()
    {
        var cut = RenderEnglish();
        WaitForChip(cut);

        Assert.Contains(English, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(Norwegian, cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_WhenTheHostMountsWithEnGbAndTheOptionHasAnEnglishLabel_ThenTheCheckboxReadsInEnglish()
    {
        var cut = RenderEnglish();
        WaitForChip(cut);

        cut.FindAll("button[aria-expanded]").First(b => b.TextContent.StartsWith("Advanced", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input[type=checkbox]")));

        Assert.Contains(cut.FindAll("label"), l => l.TextContent.Contains(English, StringComparison.Ordinal));
        Assert.DoesNotContain(cut.FindAll("label"), l => l.TextContent.Contains(Norwegian, StringComparison.Ordinal));
    }
}
