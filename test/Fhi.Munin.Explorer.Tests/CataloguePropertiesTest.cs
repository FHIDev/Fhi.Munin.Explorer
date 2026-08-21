using System.Globalization;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The rules for turning a payload's properties into something a person can read.
/// </summary>
/// <remarks>
/// These were measured against Runa rather than designed, so they are tested head-on rather than
/// inferred from rendered markup. A source there carries 73 properties across 13 groups and shows 8.
/// </remarks>
public class CataloguePropertiesTest
{
    private static PropertyMetadataEntry Entry(
        string key,
        int sortOrder,
        string group,
        string? optionsJson = null,
        string? english = null,
        string? englishGroup = null)
    {
        var name = new Dictionary<string, string> { ["no"] = key };
        var groups = new Dictionary<string, string> { ["no"] = group };

        if (english is not null)
        {
            name["en"] = english;
        }

        if (englishGroup is not null)
        {
            groups["en"] = englishGroup;
        }

        return new PropertyMetadataEntry
        {
            Key = key,
            SortOrder = sortOrder,
            GroupTranslations = groups,
            DisplayNameTranslations = name,
            OptionsJson = optionsJson,
        };
    }

    [Fact]
    public void Groups_WhenEveryKeyInAGroupIsEmpty_ThenTheGroupIsNotDrawnAtAll()
    {
        // A source carries far more curated keys than any one source fills in. Drawing the empty
        // ones gives a heading that promises something and rows that deliver nothing.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Opprettet", 20, "Datainnsamling"),
            Entry("Presisjon", 60, "Kvalitet"),
            Entry("Fullstendighet", 70, "Kvalitet"),
        ];

        Dictionary<string, string?> values = new() { ["Opprettet"] = "2023" };

        var groups = CatalogueProperties.Groups(metadata, values, "no");

        Assert.Equal(["Datainnsamling"], groups.Select(g => g.Name));
    }

    [Fact]
    public void Groups_WhenTwoGroupsTieOnAllTheirKeys_ThenOnlyThePopulatedOnesDecideTheOrder()
    {
        // The subtle one, and the reason this rule is written down. Both groups own a key at 20, so
        // ranking by every key leaves them tied and the order falls to however the payload happened
        // to enumerate — arbitrary, and different between two sources for no visible reason.
        // Ranking by the keys that actually have values separates them: 20 against 30.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("BeskrivelseEngelsk", 20, "Beskrivelse"),
            Entry("AnbefalteBruksomraader", 30, "Beskrivelse"),
            Entry("Opprettet", 20, "Datainnsamling"),
            Entry("Inklusjonskriterier", 40, "Datainnsamling"),
        ];

        Dictionary<string, string?> values = new()
        {
            // Beskrivelse's key at 20 is empty; Datainnsamling's is not.
            ["AnbefalteBruksomraader"] = "Forskning",
            ["Opprettet"] = "2023",
        };

        var groups = CatalogueProperties.Groups(metadata, values, "no");

        Assert.Equal(["Datainnsamling", "Beskrivelse"], groups.Select(g => g.Name));
    }

    [Fact]
    public void Groups_WhenAKeyIsCoded_ThenItsGroupCarriesTheWordNotTheCode()
    {
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Opprinnelse", 30, "Datainnsamling",
                  optionsJson: """[{"value":"5","label":"Direkte fra skjema","labelEn":"Directly from the form"}]"""),
        ];

        Dictionary<string, string?> values = new() { ["Opprinnelse"] = "5" };

        var group = Assert.Single(CatalogueProperties.Groups(metadata, values, "no"));
        var row = Assert.Single(group.Rows);

        Assert.Equal("Direkte fra skjema", row.Value);
    }

    [Fact]
    public void Groups_WhenTheReaderIsEnglish_ThenGroupNamesFollowToo()
    {
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Opprettet", 20, "Datainnsamling", english: "Created", englishGroup: "Data Collection"),
        ];

        Dictionary<string, string?> values = new() { ["Opprettet"] = "2023" };

        var group = Assert.Single(CatalogueProperties.Groups(metadata, values, "en"));

        Assert.Equal("Data Collection", group.Name);
        Assert.Equal("en", group.NameLanguage);
        Assert.Equal("Created", group.Rows[0].Label);
    }

    [Fact]
    public void Groups_WhenAGroupHasNoEnglishName_ThenTheNorwegianStandsInAndSaysSo()
    {
        // Curation is uneven. Showing the Norwegian name is better than dropping the group from an
        // English page, but it has to be marked, or a screen reader reads it as English.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Overstyring", 311, "Helsedatatilgangsorgan (overstyring)", english: "Override"),
        ];

        Dictionary<string, string?> values = new() { ["Overstyring"] = "Ja" };

        var group = Assert.Single(CatalogueProperties.Groups(metadata, values, "en"));

        Assert.Equal("Helsedatatilgangsorgan (overstyring)", group.Name);
        Assert.Equal("no", group.NameLanguage);
    }

    [Fact]
    public void Groups_WhenAKeyHasNoGroup_ThenItIsLeftOutRatherThanGivenOneOfItsOwn()
    {
        // A real source has eleven ungrouped keys. They are not a group called "other"; they are
        // keys the catalogue has not filed yet, and inventing a heading for them says otherwise.
        List<PropertyMetadataEntry> metadata =
        [
            new()
            {
                Key = "Ufilert",
                SortOrder = 10,
                DisplayNameTranslations = new Dictionary<string, string> { ["no"] = "Ufilert" },
            },
            Entry("Opprettet", 20, "Datainnsamling"),
        ];

        Dictionary<string, string?> values = new() { ["Ufilert"] = "noe", ["Opprettet"] = "2023" };

        var groups = CatalogueProperties.Groups(metadata, values, "no");

        Assert.Equal(["Datainnsamling"], groups.Select(g => g.Name));
    }

    [Fact]
    public void Formatting_WhenTheHostHasNoCultureOfThatName_ThenItIsTheInvariantOneRatherThanAThrow()
    {
        // The branch no host running this suite can otherwise take. It exists for a host built with
        // InvariantGlobalization, where PredefinedCulturesOnly makes every name fail — including
        // "nb-NO" and "en" — and that switch is set at build time, so it cannot be turned on
        // in-process. Reached the other way round instead, with a name no host resolves either way.
        // Left unreached, the fix ships unverified in both directions, and the failure it prevents
        // is a TypeInitializationException, which cannot be retried once thrown.
        Assert.Same(CultureInfo.InvariantCulture, CatalogueProperties.Formatting("not a culture name"));
    }

    [Fact]
    public void CatalogueOrder_WhenACultureHasFailedToResolve_ThenTheTypeStillInitialises()
    {
        // The second direction. A throw out of the initialiser above takes this field with it and
        // every property row on the page, so the assertion worth making is not what it sorts but
        // that touching it at all comes back.
        Assert.NotNull(CatalogueProperties.CatalogueOrder);
        Assert.Equal(0, CatalogueProperties.CatalogueOrder.Compare("Ås", "Ås"));
    }
}
