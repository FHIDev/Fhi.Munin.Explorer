using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// The form's code words follow the facet panel's rule: a fallback word carries its language, a bare code carries none.
public class AdvancedSearchCodeWordTest : ExplorerTestContext
{
    private sealed class Client(string optionsJson) : EmptyMuninExplorerClient
    {
        public TaskCompletionSource? HeldVocabulary { get; init; }

        public override Task<IReadOnlyList<ConditionField>> GetConditionFieldsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonSerializer.Deserialize<IReadOnlyList<ConditionField>>(TestData.Read("vilkar.json"), MuninExplorerClient.Json)!);

        public override async Task<IReadOnlyList<PropertyMetadataEntry>> GetKildePropertyMetadataAsync(CancellationToken cancellationToken = default)
        {
            if (HeldVocabulary is { } held)
            {
                await held.Task;
            }

            return [new PropertyMetadataEntry { Key = AccessLevels.Key, OptionsJson = optionsJson }];
        }
    }

    private IElement CheckboxWord(string optionsJson, string code)
    {
        Services.AddSingleton<IMuninExplorerClient>(new Client(optionsJson));
        Services.AddSingleton(new AdvancedSearchSwitch(true));
        var cut = Render<VariableSearch>(b => b
            .Add(c => c.Language, "en")
            .Add(c => c.Filter, VariableFilter.Parse($"vilkar=tilgangsniva%3AerEnAv%3A{code}")));
        cut.WaitForAssertion(() => Assert.NotNull(
            cut.FindAll("button[aria-expanded]").FirstOrDefault(b => b.TextContent.StartsWith("Advanced", StringComparison.Ordinal))));
        cut.FindAll("button[aria-expanded]").First(b => b.TextContent.StartsWith("Advanced", StringComparison.Ordinal)).Click();

        IElement? word = null;
        cut.WaitForAssertion(() =>
        {
            word = cut.FindAll("label span").FirstOrDefault(span =>
                span.ParentElement?.QuerySelector("input[type=checkbox]")?.HasAttribute("checked") == true);
            Assert.NotNull(word);
        });
        return word!;
    }

    [Fact]
    public void Form_WhenAnOptionHasOnlyANorwegianLabel_ThenTheWordIsMarkedNorwegian()
    {
        var word = CheckboxWord("""[{"value":"apne-data","label":"Åpne data"}]""", "apne-data");

        Assert.Equal("Åpne data", word.TextContent.Trim());
        Assert.Equal("no", word.GetAttribute("lang"));
    }

    [Fact]
    public void Form_WhenAnOptionHasNoLabelAtAll_ThenTheCodeIsShownUnmarked()
    {
        var word = CheckboxWord("""[{"value":"apne-data"}]""", "apne-data");

        Assert.Equal("apne-data", word.TextContent.Trim());
        Assert.Null(word.GetAttribute("lang"));
    }

    [Fact]
    public async Task Open_WhenTheVocabularyIsStillLoadingForTheLink_ThenTheRowsWaitForItsWords()
    {
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Services.AddSingleton<IMuninExplorerClient>(
            new Client("""[{"value":"apne-data","label":"Åpne data","labelEn":"Open data"}]""") { HeldVocabulary = held });
        Services.AddSingleton(new AdvancedSearchSwitch(true));
        var cut = Render<VariableSearch>(b => b
            .Add(c => c.Language, "en")
            .Add(c => c.Filter, VariableFilter.Parse("vilkar=tilgangsniva%3AerEnAv%3Aapne-data")));
        cut.WaitForAssertion(() => Assert.NotNull(
            cut.FindAll("button[aria-expanded]").FirstOrDefault(b => b.TextContent.StartsWith("Advanced", StringComparison.Ordinal))));

        cut.FindAll("button[aria-expanded]").First(b => b.TextContent.StartsWith("Advanced", StringComparison.Ordinal)).Click();
        Assert.Empty(cut.FindAll(".munin-explorer-advanced-search input[type=checkbox]"));
        await cut.InvokeAsync(held.SetResult);

        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll(".munin-explorer-advanced-search label span"), span => span.TextContent.Trim() == "Open data"));
    }
}
