using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The catalogue's free-text metadata values that are authored with markdown links and
/// <c>&lt;br&gt;</c>, rendered through the path every detail page and Runa's row panel share.
/// </summary>
public class AuthoredPropertiesTest : ExplorerTestContext
{
    private static PropertyMetadataEntry Entry(string key, int sortOrder, string type = "Text") =>
        new()
        {
            Key = key,
            SortOrder = sortOrder,
            Type = type,
            GroupTranslations = new Dictionary<string, string> { ["no"] = "Metadata" },
            DisplayNameTranslations = new Dictionary<string, string> { ["no"] = key },
        };

    private IRenderedComponent<IComponent> RenderGroup(
        IReadOnlyList<PropertyMetadataEntry> metadata, Dictionary<string, string?> values)
    {
        var group = Assert.Single(CatalogueProperties.Groups(metadata, values, ReaderLanguage.Norwegian));

        return Render(DetailBlocks.Group(group, 3, ReaderLanguage.Norwegian));
    }

    private static IElement Cell(IRenderedComponent<IComponent> cut, string label) =>
        cut.FindAll("dl > div")
           .FirstOrDefault(row => row.QuerySelector("dt")?.TextContent == label)
           ?.QuerySelector("dd")
        ?? throw new InvalidOperationException($"No '{label}' row in the group.");

    [Fact]
    public void Group_WhenAListedKeyCarriesBrAndAMarkdownLink_ThenTheyRenderAsABreakAndAnAnchor()
    {
        // K_TR's and K_LMR's values as the catalogue stores them (Fhi.Metadata-x0etk). Fails with
        // the key list emptied: the dd then holds the literal source.
        var cut = RenderGroup(
            [Entry("BeskrivelseEngelsk", 10), Entry("Kvalitetsnote", 20)],
            new()
            {
                ["BeskrivelseEngelsk"] = "The Tromsø Study.<br><br>Seven surveys.",
                ["Kvalitetsnote"] = "See [10.1093/ije/dyr049](https://doi.org/10.1093/ije/dyr049).",
            });

        var english = Cell(cut, "BeskrivelseEngelsk");

        Assert.Equal(2, english.QuerySelectorAll("br").Length);
        Assert.DoesNotContain("<br>", english.TextContent, StringComparison.Ordinal);

        var link = Assert.Single(Cell(cut, "Kvalitetsnote").QuerySelectorAll("a"));

        Assert.Equal("https://doi.org/10.1093/ije/dyr049", link.GetAttribute("href"));
        Assert.Equal("10.1093/ije/dyr049", link.TextContent);
    }

    [Fact]
    public void Group_WhenAListedKeyHoldsPlainNewlines_ThenEachLineBreaksRatherThanRunningTogether()
    {
        // Runa's Kommentar: about one variable in ten separates its lines with bare newlines, which
        // a dd collapses into one run ("Kilde: FEST …  Eksempel: …").
        var cut = RenderGroup(
            [Entry("Kommentar", 10)],
            new() { ["Kommentar"] = "Kilde: FEST.\nEksempel: Oral." });

        Assert.Single(Cell(cut, "Kommentar").QuerySelectorAll("br"));
    }

    [Fact]
    public void Group_WhenAListedKeyHasNoMarkup_ThenItRendersAsTheSameSingleTextNodeAsBefore()
    {
        // Passes before the change too; it pins that the renderer leaves ordinary prose alone.
        var cut = RenderGroup(
            [Entry("Kvalitetsnote", 10)],
            new() { ["Kvalitetsnote"] = "Dekningsgrad 95 % i 2023." });

        Assert.Equal("Dekningsgrad 95 % i 2023.", Cell(cut, "Kvalitetsnote").InnerHtml);
    }

    [Fact]
    public void Group_WhenAnUnlistedKeyCarriesTheSameMarkup_ThenItStaysLiteralText()
    {
        // The list decides, not the type: Formaal is Text as well, and nobody authored markup in it.
        var cut = RenderGroup(
            [Entry("Formaal", 10), Entry("Kvalitetsnote", 20)],
            new()
            {
                ["Formaal"] = "[Lenke](https://example.org)<br>",
                ["Kvalitetsnote"] = "[Lenke](https://example.org)",
            });

        var plain = Cell(cut, "Formaal");

        Assert.Empty(plain.QuerySelectorAll("a, br"));
        Assert.Equal("[Lenke](https://example.org)<br>", plain.TextContent);
        Assert.Single(Cell(cut, "Kvalitetsnote").QuerySelectorAll("a"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("[klikk](javascript:alert(1))")]
    [InlineData("# Overskrift")]
    public void Group_WhenAListedKeyCarriesHostileInput_ThenItRendersAsTextAndNoElement(string hostile)
    {
        // CatalogueMarkdownTest's hardening, reached through the new call site rather than assumed.
        var cut = RenderGroup([Entry("Kvalitetsnote", 10)], new() { ["Kvalitetsnote"] = hostile });

        var cell = Cell(cut, "Kvalitetsnote");

        Assert.Empty(cell.Children);
        Assert.Equal(hostile, cell.TextContent);
    }

    [Fact]
    public void Group_WhenATaggedListEntryCarriesALink_ThenEachLanguageRendersIt()
    {
        // FormaalFlerspraklig is the one listed key whose rows take the per-language branch.
        var cut = RenderGroup(
            [Entry("FormaalFlerspraklig", 10, type: "LangTaggedList")],
            new()
            {
                ["FormaalFlerspraklig"] =
                    """
                    [{"value":"Se [NOIS](https://www.fhi.no/nois)","language":"nb"},
                     {"value":"See [NOIS](https://www.fhi.no/en/nois)","language":"en"}]
                    """,
            });

        Assert.Equal(
            ["https://www.fhi.no/nois", "https://www.fhi.no/en/nois"],
            cut.FindAll("dd span a").Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void AuthoredKeys_LeaveOutBeskrivelseFlerspraklig_BecauseThePageIngressAlreadyRendersIt()
    {
        Assert.DoesNotContain("BeskrivelseFlerspraklig", CatalogueProperties.AuthoredKeys);
    }
}
