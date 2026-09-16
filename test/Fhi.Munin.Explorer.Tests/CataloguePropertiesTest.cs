using System.Collections.ObjectModel;
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
    // Most types carry one language, and a row that quietly grew a second is the defect this
    // suite is now guarding, so the count is asserted rather than indexed past.
    private static LocalisedText Only(PropertyRow row) => Assert.Single(row.Values);

    private static PropertyMetadataEntry Entry(
        string key,
        int sortOrder,
        string group,
        string? optionsJson = null,
        string? english = null,
        string? englishGroup = null,
        string type = "",
        string? groupKey = null,
        int? groupSortOrder = null)
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
            GroupKey = groupKey,
            GroupSortOrder = groupSortOrder,
            DisplayNameTranslations = name,
            OptionsJson = optionsJson,
            Type = type,
        };
    }

    // ---------------------------------------------------------------------------------
    // Section identity and section order. Both used to be read off the data — the heading
    // said which section an entry was in, and the members' sort orders said where it went.
    // Each rule below is paired with the fallback the same payload takes without the field,
    // because this package meets APIs older than either (Fhi.Metadata-35w0p.19).
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Groups_WhenOneLanguageRenamesTheSectionButTheKeyDoesNot_ThenItsPropertiesStayInOneSection()
    {
        // The rename test. A curator editing the Norwegian title of a section is not creating a
        // second section, and a reader on the other language is not looking at a different page.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Beskrivelse", 1001, "Om registeret", groupKey: "om-registeret"),
            Entry("Formaal", 1002, "Om kilden", groupKey: "om-registeret"),
        ];

        Dictionary<string, string?> values = new() { ["Beskrivelse"] = "tekst", ["Formaal"] = "tekst" };

        var renamed = CatalogueProperties.Groups(metadata, values, "no");

        // The same payload with the rename never made, which is the "before" half of the count.
        var untouched = CatalogueProperties.Groups(
            [Entry("Beskrivelse", 1001, "Om registeret", groupKey: "om-registeret"),
             Entry("Formaal", 1002, "Om registeret", groupKey: "om-registeret")],
            values, "no");

        Assert.Equal(untouched.Count, renamed.Count);
        Assert.Equal(2, Assert.Single(renamed).Rows.Count);
    }

    [Fact]
    public void Groups_WhenThePayloadPredatesTheGroupKey_ThenARenameStillSplitsTheSection()
    {
        // The fallback, asserted rather than assumed. Against an API with no groupKey the heading
        // is the only identity there is, so a rename does split the section — which is why this is
        // the path a payload takes only when it has nothing better to offer.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Beskrivelse", 1001, "Om registeret"),
            Entry("Formaal", 1002, "Om kilden"),
        ];

        Dictionary<string, string?> values = new() { ["Beskrivelse"] = "tekst", ["Formaal"] = "tekst" };

        Assert.Equal(["Om registeret", "Om kilden"],
                     CatalogueProperties.Groups(metadata, values, "no").Select(g => g.Name));
    }

    [Fact]
    public void Groups_WhenTwoSectionsShareAHeadingButNotAKey_ThenTheyAreDrawnApart()
    {
        // The other direction of the same rule. Two sections a curator has titled the same way are
        // still two sections, and merging them on the heading would move a property into a section
        // nobody filed it in.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Beskrivelse", 1001, "Innhold", groupKey: "innhold-kilde"),
            Entry("Formaal", 2001, "Innhold", groupKey: "innhold-samling"),
        ];

        Dictionary<string, string?> values = new() { ["Beskrivelse"] = "tekst", ["Formaal"] = "tekst" };

        var groups = CatalogueProperties.Groups(metadata, values, "no");

        Assert.Equal(["Innhold", "Innhold"], groups.Select(g => g.Name));
        Assert.All(groups, group => Assert.Single(group.Rows));
    }

    [Fact]
    public void Groups_WhenTheCatalogueOrdersTheSections_ThenFillingAnEmptyPropertyDoesNotMoveThem()
    {
        // The stability test, and the fixture below is this one with the two new fields taken off.
        // Epost sorts above every other member, so filling it in drags Kontakt to the top of the
        // page under the inferred order. The catalogue's own order does not move for it.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Beskrivelse", 9001, "Om registeret", groupKey: "om-registeret", groupSortOrder: 1000),
            Entry("Kontaktperson", 9500, "Kontakt", groupKey: "kontakt", groupSortOrder: 6000),
            Entry("Epost", 1, "Kontakt", groupKey: "kontakt", groupSortOrder: 6000),
        ];

        Dictionary<string, string?> before = new() { ["Beskrivelse"] = "tekst", ["Kontaktperson"] = "Kari" };
        Dictionary<string, string?> after = new(before) { ["Epost"] = "kari@fhi.no" };

        Assert.Equal(["Om registeret", "Kontakt"],
                     CatalogueProperties.Groups(metadata, before, "no").Select(g => g.Name));
        Assert.Equal(["Om registeret", "Kontakt"],
                     CatalogueProperties.Groups(metadata, after, "no").Select(g => g.Name));
    }

    [Fact]
    public void Groups_WhenThePayloadPredatesTheGroupSortOrder_ThenFillingAnEmptyPropertyStillMovesThem()
    {
        // The fallback the test above replaces, and the discriminator for it: an older API still
        // gets this, and a section that moves because somebody typed an address into a field is
        // what the catalogue's own order exists to stop.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Beskrivelse", 9001, "Om registeret"),
            Entry("Kontaktperson", 9500, "Kontakt"),
            Entry("Epost", 1, "Kontakt"),
        ];

        Dictionary<string, string?> before = new() { ["Beskrivelse"] = "tekst", ["Kontaktperson"] = "Kari" };
        Dictionary<string, string?> after = new(before) { ["Epost"] = "kari@fhi.no" };

        Assert.Equal(["Om registeret", "Kontakt"],
                     CatalogueProperties.Groups(metadata, before, "no").Select(g => g.Name));
        Assert.Equal(["Kontakt", "Om registeret"],
                     CatalogueProperties.Groups(metadata, after, "no").Select(g => g.Name));
    }

    [Fact]
    public void Groups_WhenOnlySomeSectionsCarryTheCataloguesOrder_ThenThePlacedOnesLeadAsABlock()
    {
        // A payload part-way through the rollout carries two numbering spaces — a section's own
        // band and a property's position inside one — and comparing them would order the page on
        // arithmetic nobody chose. The placed sections lead instead, in the order they were given.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Kommentar", 10, "Uplassert"),
            Entry("Beskrivelse", 9001, "Om registeret", groupKey: "om-registeret", groupSortOrder: 1000),
        ];

        Dictionary<string, string?> values = new() { ["Kommentar"] = "tekst", ["Beskrivelse"] = "tekst" };

        Assert.Equal(["Om registeret", "Uplassert"],
                     CatalogueProperties.Groups(metadata, values, "no").Select(g => g.Name));
    }

    [Fact]
    public void Groups_WhenAKeyedEntryIsFiledUnderNoSection_ThenItIsStillDrawnNowhere()
    {
        // The column-backed keys arrive this way, and each detail view already draws them in markup
        // of its own. Gathering them under a catch-all would state the same fact a second time in a
        // second word, which is the duplication drawnElsewhere exists to prevent.
        List<PropertyMetadataEntry> metadata =
        [
            new()
            {
                Key = "Ufilert",
                SortOrder = 10,
                GroupKey = "om-registeret",
                GroupSortOrder = 1000,
                DisplayNameTranslations = new Dictionary<string, string> { ["no"] = "Ufilert" },
            },
            Entry("Beskrivelse", 1001, "Om registeret", groupKey: "om-registeret", groupSortOrder: 1000),
        ];

        Dictionary<string, string?> values = new() { ["Ufilert"] = "noe", ["Beskrivelse"] = "tekst" };

        var group = Assert.Single(CatalogueProperties.Groups(metadata, values, "no"));

        Assert.Equal(["Beskrivelse"], group.Rows.Select(row => row.Label));
    }

    [Fact]
    public void Groups_WhenOneKeyedSectionIsTitledTwoWays_ThenTheFirstTitleHeadsIt()
    {
        // A payload disagreeing with itself still has to draw one section under one heading, and
        // which one has to be decided rather than left to whichever entry the merge happened to
        // reach last. First sighting, so the answer follows the order the payload was written in.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Beskrivelse", 1001, "Om registeret", groupKey: "om-registeret"),
            Entry("Formaal", 1002, "Om kilden", groupKey: "om-registeret"),
        ];

        Dictionary<string, string?> values = new() { ["Beskrivelse"] = "tekst", ["Formaal"] = "tekst" };

        Assert.Equal("Om registeret", Assert.Single(CatalogueProperties.Groups(metadata, values, "no")).Name);
    }

    [Fact]
    public void Groups_WhenTheSectionKeyIsBlank_ThenTheHeadingIdentifiesItAsThoughTheFieldWereAbsent()
    {
        // An empty string is how a payload says "no key", not a key every unkeyed section shares —
        // read the other way, every unkeyed section on the page would merge into one.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Beskrivelse", 1001, "Om registeret", groupKey: "  "),
            Entry("Kontaktperson", 6001, "Kontakt", groupKey: ""),
        ];

        Dictionary<string, string?> values = new() { ["Beskrivelse"] = "tekst", ["Kontaktperson"] = "Kari" };

        Assert.Equal(["Om registeret", "Kontakt"],
                     CatalogueProperties.Groups(metadata, values, "no").Select(g => g.Name));
    }

    [Fact]
    public void Groups_WhenOneSectionCarriesAKeyAndAnotherOfTheSameNameDoesNot_ThenTheyAreDrawnApart()
    {
        // The mixed half of the identity rule, and the shape a half-migrated payload takes: a key
        // and a heading are different claims, so matching one against the other would merge a
        // section the payload named with one it only titled.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Beskrivelse", 1001, "Innhold", groupKey: "innhold"),
            Entry("Formaal", 2001, "Innhold"),
        ];

        Dictionary<string, string?> values = new() { ["Beskrivelse"] = "tekst", ["Formaal"] = "tekst" };

        var groups = CatalogueProperties.Groups(metadata, values, "no");

        Assert.Equal(["Innhold", "Innhold"], groups.Select(g => g.Name));
        Assert.All(groups, group => Assert.Single(group.Rows));
    }

    [Fact]
    public void Groups_WhenOneSectionsEntriesDisagreeAboutTheCataloguesOrder_ThenTheFirstOfThemPlacesIt()
    {
        // Whether a section is placed at all is decided by the entry that opened it, null included.
        // Reading on until something non-null arrives would let a straggler lift a half-migrated
        // section out of the inferred band, and which straggler depends on the payload's order.
        List<PropertyMetadataEntry> placedLast =
        [
            Entry("Beskrivelse", 10, "Ustabil", groupKey: "ustabil"),
            Entry("Formaal", 11, "Ustabil", groupKey: "ustabil", groupSortOrder: 1000),
            Entry("Kontaktperson", 9500, "Plassert", groupKey: "plassert", groupSortOrder: 5000),
            Entry("Kommentar", 20, "Uplassert"),
        ];

        List<PropertyMetadataEntry> placedFirst =
        [
            Entry("Beskrivelse", 10, "Ustabil", groupKey: "ustabil", groupSortOrder: 1000),
            Entry("Formaal", 11, "Ustabil", groupKey: "ustabil"),
            Entry("Kontaktperson", 9500, "Plassert", groupKey: "plassert", groupSortOrder: 5000),
            Entry("Kommentar", 20, "Uplassert"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["Beskrivelse"] = "tekst",
            ["Formaal"] = "tekst",
            ["Kontaktperson"] = "Kari",
            ["Kommentar"] = "tekst",
        };

        // Opened by an entry the catalogue never placed, so the section stays in the inferred band
        // and sorts on its members — ahead of Uplassert, behind every placed section.
        Assert.Equal(["Plassert", "Ustabil", "Uplassert"],
                     CatalogueProperties.Groups(placedLast, values, "no").Select(g => g.Name));

        // The same four entries, the section's own two the other way round: now it is placed at
        // 1000 and leads the section the catalogue put at 5000.
        Assert.Equal(["Ustabil", "Plassert", "Uplassert"],
                     CatalogueProperties.Groups(placedFirst, values, "no").Select(g => g.Name));
    }

    [Fact]
    public void Groups_WhenTheEntryThatOpensASectionDrawsNoRow_ThenItPlacesTheSectionAnyway()
    {
        // Metadata lists every key a section can hold and a payload fills some, so the entry that
        // opens one need not be the first to draw. Deciding on the first that does would read as
        // the same rule and would lift this section out of the inferred band on a straggler.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Beskrivelse", 10, "Ustabil", groupKey: "ustabil"),
            Entry("Formaal", 11, "Ustabil", groupKey: "ustabil", groupSortOrder: 1000),
            Entry("Kontaktperson", 9500, "Plassert", groupKey: "plassert", groupSortOrder: 5000),
            Entry("Kommentar", 20, "Uplassert"),
        ];

        // Beskrivelse opens the section and is unset, so the section is unplaced and sorts on the
        // one member that does draw: ahead of Uplassert at 20, behind every placed section.
        Dictionary<string, string?> values = new()
        {
            ["Formaal"] = "tekst",
            ["Kontaktperson"] = "Kari",
            ["Kommentar"] = "tekst",
        };

        Assert.Equal(["Plassert", "Ustabil", "Uplassert"],
                     CatalogueProperties.Groups(metadata, values, "no").Select(g => g.Name));
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

        Assert.Equal("Direkte fra skjema", Only(row).Text);
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

    [Theory]
    [InlineData("Formål (språkmerket)", "no", "Formål")]
    [InlineData("Purpose (language-tagged)", "en", "Purpose")]
    [InlineData("Tittel (flerspråklig)", "no", "Tittel")]
    [InlineData("Title (multilingual)", "en", "Title")]
    public void Rows_WhenTheLabelCarriesTheCataloguesStorageQualifier_ThenTheReaderNeverSeesIt(
        string curatedLabel, string reader, string expected)
    {
        // "språkmerket"/"flerspråklig" say how the catalogue STORES a value, not something a reader
        // needs — stripped at render time in both curated languages (Fhi.Metadata-43jrq).
        List<PropertyMetadataEntry> metadata =
        [
            new()
            {
                Key = "X",
                SortOrder = 10,
                GroupTranslations = new Dictionary<string, string> { ["no"] = "Gruppe" },
                DisplayNameTranslations = new Dictionary<string, string> { [reader] = curatedLabel },
            },
        ];

        Dictionary<string, string?> values = new() { ["X"] = "verdi" };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, reader));

        Assert.Equal(expected, row.Label);
    }

    [Fact]
    public void Groups_WhenTheGroupNameCarriesTheCataloguesStorageQualifier_ThenItIsStrippedToo()
    {
        List<PropertyMetadataEntry> metadata = [Entry("Opprettet", 20, "Merknad (flerspråklig)")];
        Dictionary<string, string?> values = new() { ["Opprettet"] = "2023" };

        var group = Assert.Single(CatalogueProperties.Groups(metadata, values, "no"));

        Assert.Equal("Merknad", group.Name);
    }

    [Fact]
    public void Rows_WhenTheCuratedLabelIsOnlyTheStorageQualifier_ThenTheRowIsDroppedRatherThanBlank()
    {
        // A label that is nothing but the qualifier strips to the empty string, not to prose. An
        // empty <dt> would be worse than the qualifier it replaced, so the row goes instead
        // (Fhi.Metadata-43jrq).
        List<PropertyMetadataEntry> metadata =
        [
            new()
            {
                Key = "X",
                SortOrder = 10,
                GroupTranslations = new Dictionary<string, string> { ["no"] = "Gruppe" },
                DisplayNameTranslations = new Dictionary<string, string> { ["no"] = " (språkmerket)" },
            },
        ];

        Dictionary<string, string?> values = new() { ["X"] = "verdi" };

        Assert.Empty(CatalogueProperties.Rows(metadata, values, "no"));
    }

    [Fact]
    public void Groups_WhenTheCuratedGroupNameIsOnlyTheStorageQualifier_ThenTheGroupIsDroppedRatherThanBlank()
    {
        List<PropertyMetadataEntry> metadata =
        [
            new()
            {
                Key = "X",
                SortOrder = 10,
                GroupTranslations = new Dictionary<string, string> { ["no"] = " (flerspråklig)" },
                DisplayNameTranslations = new Dictionary<string, string> { ["no"] = "Felt" },
            },
        ];

        Dictionary<string, string?> values = new() { ["X"] = "verdi" };

        Assert.Empty(CatalogueProperties.Groups(metadata, values, "no"));
    }

    [Fact]
    public void Rows_WhenTheBagIsNull_ThenThereAreNoRowsRatherThanAThrow()
    {
        // Every contract declares AdditionalProperties non-nullable with an initialiser, and
        // System.Text.Json writes null straight over it for an explicit "additionalProperties":
        // null. All three call sites pass such a field — KildeView, VariableView and the variable
        // panel's rows — so the question is answered here rather than three times over.
        //
        // Empty is the right reading, not merely the safe one: the payload is saying the source has
        // no curated properties, which is what an empty bag says too.
        Assert.Empty(CatalogueProperties.Rows([Entry("Opprettet", 20, "Datainnsamling")], null, "no"));
    }

    [Fact]
    public void Groups_WhenTheBagIsNull_ThenThereAreNoGroupsRatherThanAThrow()
    {
        // Normalised in Groups as well as in Rows, because the group ordering reads the bag itself
        // rather than going through Rows. Guarding only Rows leaves that second read to fall over
        // the moment a group has any row at all.
        Assert.Empty(CatalogueProperties.Groups([Entry("Opprettet", 20, "Datainnsamling")], null, "no"));
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

    /// <summary>The vocabulary <c>healthTheme</c> carries, trimmed to the codes these tests use.</summary>
    private const string HealthThemes =
        """
        [{"value":"healthdcatap:pharmaceuticals","label":"Legemidler","labelEn":"Pharmaceuticals"},
         {"value":"healthdcatap:rare-diseases","label":"Sjeldne sykdommer","labelEn":"Rare diseases"}]
        """;

    [Fact]
    public void Rows_WhenAValueIsAMultilingualEnvelope_ThenEveryLanguageIsDrawnAsProseTheReadersFirst()
    {
        // The shape the API really sends: the object is serialised into the string field, so a view
        // that draws the bag verbatim draws braces, key names and escapes at the reader.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("TittelFlerspraklig", 540, "EHDS / HealthDCAT-AP", type: "MultilingualText"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["TittelFlerspraklig"] = """{"nb":"The Tromsø study","en":"The Tromsø Study"}""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "en"));

        // Both slots, the reader's first. Resolving to one dropped the other with nothing on the
        // page able to reach it: the toggle only offers the two languages the page itself has.
        Assert.Equal(
            [("The Tromsø Study", "en"), ("The Tromsø study", "no")],
            row.Values.Select(v => (v.Text, v.Language)));
    }

    [Fact]
    public void Rows_WhenAMultilingualEnvelopeHasNoEnglish_ThenTheNorwegianShowsAndSaysSoItself()
    {
        // 130 of these across the catalogue carry nb and only 39 carry en, so the fallback is the
        // common path rather than the edge — and the language it lands in is the whole point.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("TittelFlerspraklig", 540, "EHDS / HealthDCAT-AP", type: "MultilingualText"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["TittelFlerspraklig"] = """{"nb":"Nasjonalt register for ablasjonsbehandling"}""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "en"));

        Assert.Equal("Nasjonalt register for ablasjonsbehandling", Only(row).Text);
        Assert.Equal("no", Only(row).Language);
    }

    [Fact]
    public void Rows_WhenAListCarriesItsOwnLanguageTags_ThenTheReadersEntriesAreDrawnAsOneValue()
    {
        // A different envelope for the same problem: a list of values each tagged with its own
        // language. Lists really are lists here — the catalogue holds up to sixteen entries in one.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("FormaalFlerspraklig", 131, "EHDS / HealthDCAT-AP", type: "LangTaggedList"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["FormaalFlerspraklig"] =
                """[{"value":"Kvalitetsforbedring","language":"nb"},{"value":"Forskning","language":"nb"}]""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal("Kvalitetsforbedring; Forskning", Only(row).Text);
        Assert.Equal("no", Only(row).Language);
    }

    [Fact]
    public void Rows_WhenATaggedListCarriesBothLanguages_ThenOneReadersListIsNotSplicedIntoTheOthers()
    {
        // Gathered per language rather than per entry, so an English reader gets the English list
        // whole. Entry by entry, a language with fewer entries would borrow the other's.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("FormaalFlerspraklig", 131, "EHDS / HealthDCAT-AP", type: "LangTaggedList"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["FormaalFlerspraklig"] =
                """
                [{"value":"Kvalitetsforbedring","language":"nb"},
                 {"value":"Forskning","language":"nb"},
                 {"value":"Research","language":"en"}]
                """,
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "en"));

        // Both lists, each whole and each tagged, rather than the reader's alone.
        Assert.Equal(
            [("Research", "en"), ("Kvalitetsforbedring; Forskning", "no")],
            row.Values.Select(v => (v.Text, v.Language)));
    }

    [Fact]
    public void Rows_WhenATaggedListSpellsNorwegianBothWays_ThenTheEntriesJoinRatherThanOneWinning()
    {
        // A tagged list's buckets are list items, not translations of one another, so two spellings
        // of Norwegian have to be concatenated. Gathered by raw tag they arrived at the resolver as
        // two slots of one language, and everything after the first was silently dropped —
        // curated values no toggle could reach, in the bead that exists to stop exactly that
        // (PR 149 review).
        List<PropertyMetadataEntry> metadata =
        [
            Entry("FormaalFlerspraklig", 131, "EHDS / HealthDCAT-AP", type: "LangTaggedList"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["FormaalFlerspraklig"] =
                """
                [{"value":"Forskning","language":"no"},
                 {"value":"Kvalitetsforbedring","language":"nb"},
                 {"value":"Research","language":"en"}]
                """,
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal(
            [("Forskning; Kvalitetsforbedring", "no"), ("Research", "en")],
            row.Values.Select(v => (v.Text, v.Language)));
    }

    [Fact]
    public void Rows_WhenALangTaggedValueIsPlainTextInstead_ThenItIsShownAsItArrived()
    {
        // The catalogue is not consistent about this type: 69 values arrive as tagged arrays, 33 as
        // plain text and one as a semicolon list. A value that is not the shape its type promises is
        // still a value, and dropping it would hide that the two disagree.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("hasLegalBasis", 320, "EHDS / HealthDCAT-AP", type: "LangTaggedList"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["hasLegalBasis"] = "§ 9 Registre som er samtykkebaserte",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal("§ 9 Registre som er samtykkebaserte", Only(row).Text);
        Assert.Equal("no", Only(row).Language);
    }

    [Fact]
    public void Rows_WhenAMultiSelectHoldsSeveralCodes_ThenEachIsResolvedThroughTheVocabulary()
    {
        // The vocabulary lookup matches on the whole stored value, which is right for one code and
        // wrong for a list: the array's own text matches nothing, and the array reaches the page.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("healthTheme", 305, "EHDS / HealthDCAT-AP", optionsJson: HealthThemes, type: "MultiSelect"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["healthTheme"] = """["healthdcatap:pharmaceuticals","healthdcatap:rare-diseases"]""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal("Legemidler; Sjeldne sykdommer", Only(row).Text);
        Assert.Equal("no", Only(row).Language);
    }

    [Fact]
    public void Rows_WhenAMultiSelectCodeIsNotInTheVocabulary_ThenItIsShownRatherThanDropped()
    {
        // Half a list is worse than a list with a code in it: the reader cannot tell that a value
        // was left out, and the row would claim the source has fewer themes than it does.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("healthTheme", 305, "EHDS / HealthDCAT-AP", optionsJson: HealthThemes, type: "MultiSelect"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["healthTheme"] = """["healthdcatap:pharmaceuticals","healthdcatap:not-curated-yet"]""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal("Legemidler; healthdcatap:not-curated-yet", Only(row).Text);
    }

    [Fact]
    public void Rows_WhenAPropertyHoldsAnObject_ThenTheRowIsDroppedRatherThanFilledWithJson()
    {
        // creator, contactPoint and qualifiedAttribution are records with named parts, and the
        // catalogue curates a label for the property but none for what is inside it. There is no
        // honest single cell to draw, so the row goes rather than the JSON.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Formaal", 10, "Formål"),
            Entry("creator", 425, "Formål", type: "Object"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["Formaal"] = "Kvalitetsforbedring",
            ["creator"] = """{"name":"UiT","homepage":"https://uit.no"}""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal("Formaal", row.Label);
    }

    [Fact]
    public void Rows_WhenAGroupHoldsNothingButObjects_ThenTheGroupGoesWithItsRows()
    {
        // Dropping a row has to drop an empty group the same way an unfilled key does, or the page
        // grows a heading promising something with nothing under it.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Formaal", 10, "Formål"),
            Entry("creator", 425, "Ansvar", type: "Object"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["Formaal"] = "Kvalitetsforbedring",
            ["creator"] = """{"name":"UiT"}""",
        };

        var group = Assert.Single(CatalogueProperties.Groups(metadata, values, "no"));

        Assert.Equal("Formål", group.Name);
    }

    [Fact]
    public void Rows_WhenAnEnvelopeIsMalformed_ThenTheValueIsShownAsItArrived()
    {
        // Curated data arriving over the wire, so one bad value costs that value its unwrapping and
        // not the page. Shown as stored for the reason a plain-text tagged value is.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("TittelFlerspraklig", 540, "EHDS / HealthDCAT-AP", type: "MultilingualText"),
        ];

        Dictionary<string, string?> values = new() { ["TittelFlerspraklig"] = """{"nb":"unterminated""" };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal("""{"nb":"unterminated""", Only(row).Text);
        Assert.Equal("no", Only(row).Language);
    }

    [Fact]
    public void Rows_WhenAPropertyIsOrdinaryText_ThenNothingAboutItChanged()
    {
        // The types that store prose are the great majority, and the switch must leave them exactly
        // where they were — including a code the vocabulary does not list.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("Formaal", 10, "Formål", type: "Text"),
            Entry("accessRights", 300, "Formål", optionsJson: HealthThemes, type: "SingleSelect"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["Formaal"] = "Kvalitetsforbedring",
            ["accessRights"] = "eu-access:NON_PUBLIC",
        };

        var rows = CatalogueProperties.Rows(metadata, values, "no");

        Assert.Equal(["Kvalitetsforbedring", "eu-access:NON_PUBLIC"], rows.Select(r => Only(r).Text));
        Assert.Equal(["no", "no"], rows.Select(r => Only(r).Language));
    }

    [Fact]
    public void Rows_WhenAnEnvelopeHoldsALanguageBesideNorwegian_ThenTheThirdLanguageIsDrawnRatherThanDropped()
    {
        // The bag is open and the page has two languages, so a third was unreachable by
        // construction: no toggle here could ever have selected it (Fhi.Metadata-l9d5r).
        List<PropertyMetadataEntry> metadata =
        [
            Entry("TittelFlerspraklig", 540, "EHDS / HealthDCAT-AP", type: "MultilingualText"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["TittelFlerspraklig"] = """{"nb":"Kreftregisteret","de":"Krebsregister"}""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal(
            [("Kreftregisteret", "no"), ("Krebsregister", "de")],
            row.Values.Select(v => (v.Text, v.Language)));
    }

    [Fact]
    public void Rows_WhenAnEnvelopeHoldsOnlyALanguageThePackageCannotName_ThenItIsMarkedWithThatLanguage()
    {
        // The reader's tag is precisely the one Foreign drops, so returning it left the text with
        // no lang at all and a Norwegian page announced German as Norwegian. WCAG 3.1.2.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("TittelFlerspraklig", 540, "EHDS / HealthDCAT-AP", type: "MultilingualText"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["TittelFlerspraklig"] = """{"de":"Deutsches Krebsregister"}""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal("Deutsches Krebsregister", Only(row).Text);
        Assert.Equal("de", Only(row).Language);
        Assert.Equal("de", CatalogueProperties.Foreign(Only(row).Language, "no"));
    }

    [Fact]
    public void Rows_WhenAnEnvelopeHoldsOnlyEnglishAndTheReaderIsNorwegian_ThenItIsMarkedEnglish()
    {
        // The same defect one tag closer to home, and the one that is already in the catalogue:
        // 39 fields carry en, so an en-only bag is reachable today rather than hypothetically.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("TittelFlerspraklig", 540, "EHDS / HealthDCAT-AP", type: "MultilingualText"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["TittelFlerspraklig"] = """{"en":"The Cancer Registry"}""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal("The Cancer Registry", Only(row).Text);
        Assert.Equal("en", Only(row).Language);
    }

    [Fact]
    public void Rows_WhenAnEnvelopeCarriesBothNoAndNb_ThenTheyAreOneSlotAndNoWins()
    {
        // Two spellings of one language would otherwise draw the same row twice, and which text
        // won would depend on however the dictionary enumerated.
        List<PropertyMetadataEntry> metadata =
        [
            Entry("TittelFlerspraklig", 540, "EHDS / HealthDCAT-AP", type: "MultilingualText"),
        ];

        Dictionary<string, string?> values = new()
        {
            ["TittelFlerspraklig"] = """{"nb":"Fra nb","no":"Fra no"}""",
        };

        var row = Assert.Single(CatalogueProperties.Rows(metadata, values, "no"));

        Assert.Equal("Fra no", Only(row).Text);
        Assert.Equal("no", Only(row).Language);
    }

    // The one key every Placed case below asks about, and the label its row would carry. A section
    // that draws it draws exactly this <dt>, so "placed" and "drawn" are asked in the same words.
    private const string Key = "Dataansvarlig";
    private const string Label = "Dataansvarlig";
    private const string Section = "Om kilden";
    private const string Value = "St. Olavs hospital HF";

    /// <summary>One definition of <see cref="Key"/>, as far from the ordinary one as each case needs.</summary>
    private static PropertyMetadataEntry Placement(
        string? label = Label, string? group = Section, string type = "String") =>
        new()
        {
            Key = Key,
            SortOrder = 90,
            Type = type,
            DisplayNameTranslations = label is null
                ? ReadOnlyDictionary<string, string>.Empty
                : new Dictionary<string, string> { ["no"] = label },
            GroupTranslations = group is null
                ? ReadOnlyDictionary<string, string>.Empty
                : new Dictionary<string, string> { ["no"] = group },
        };

    /// <summary>A second key in a section, so a case can say what else that section holds.</summary>
    private static PropertyMetadataEntry Neighbour(string key, string group) =>
        new()
        {
            Key = key,
            SortOrder = 95,
            Type = "String",
            DisplayNameTranslations = new Dictionary<string, string> { ["no"] = key },
            GroupTranslations = new Dictionary<string, string> { ["no"] = group },
        };

    private static Dictionary<string, string?> Filled(string? value = Value) =>
        new() { [Key] = value };

    /// <summary>
    /// Every way the catalogue can carry this key, and whether a section actually ends up drawing it.
    /// </summary>
    /// <remarks>
    /// Named so a failure says which case moved, and asked of both sides at once below: the pair is
    /// the invariant, not either answer on its own.
    /// </remarks>
    public static TheoryData<string, List<PropertyMetadataEntry>, Dictionary<string, string?>, bool>
        PlacementCases => new()
        {
            { "an ordinary placed key", [Placement()], Filled(), true },
            { "a placed key the payload left blank", [Placement()], Filled("   "), false },
            { "a placed key the payload never carried", [Placement()], [], false },
            { "a placed key with no label for this reader", [Placement(label: null)], Filled(), false },
            { "a placed key labelled only the storage qualifier", [Placement(" (språkmerket)")], Filled(), false },
            { "a placed key whose type leaves nothing to draw", [Placement(type: "Object")], Filled(), false },
            { "a key the catalogue placed in no section", [Placement(group: null)], Filled(), false },
            { "a key whose section is named only the qualifier", [Placement(group: " (flerspråklig)")], Filled(), false },
            {
                "a placed key alone in a section of empty neighbours",
                [Placement(), Neighbour("Databehandler", Section)],
                Filled(),
                true
            },
            {
                "a placed key whose section name collides after stripping",
                [Placement(group: $"{Section} (flerspråklig)"), Neighbour("Databehandler", Section)],
                new Dictionary<string, string?> { [Key] = Value, ["Databehandler"] = "Hemit HF" },
                true
            },
        };

    /// <summary>
    /// Placed has to answer exactly the question Groups answers.
    /// </summary>
    /// <remarks>
    /// The two are not one code path: Placed runs Rows over a single filtered entry plus GroupName,
    /// while Groups additionally drops a group whose keys all came out empty and merges groups whose
    /// names collide. A key called placed that the grouping then drops leaves the fact box yielding
    /// to a section that draws nothing, and the fact is on no surface at all — a missing fact, which
    /// nothing else on the page reveals (Fhi.Metadata-bct95).
    /// </remarks>
    [Theory]
    [MemberData(nameof(PlacementCases))]
    public void Placed_WhateverTheCatalogueCarries_ThenItAgreesWithWhatGroupsActuallyDraws(
        string name, List<PropertyMetadataEntry> metadata, Dictionary<string, string?> values, bool expected)
    {
        var placed = CatalogueProperties.Placed(metadata, values, "no", Key);
        var drawn = CatalogueProperties.Groups(metadata, values, "no")
                                       .SelectMany(group => group.Rows)
                                       .Any(row => row.Label == Label);

        Assert.Equal((name, expected, expected), (name, placed, drawn));
    }

    [Fact]
    public void Placed_WhenTheViewDrawsTheKeyItself_ThenNoSectionIsDrawingItEither()
    {
        // drawnElsewhere is the view's own suppression and goes into both questions, so a key the
        // view has taken out of the sections is one the view must keep drawing.
        List<PropertyMetadataEntry> metadata = [Placement()];
        var values = Filled();
        IReadOnlySet<string> suppressed = new HashSet<string>(StringComparer.Ordinal) { Key };

        Assert.False(CatalogueProperties.Placed(metadata, values, "no", Key, suppressed));
        Assert.Empty(CatalogueProperties.Groups(metadata, values, "no", suppressed));
    }

    [Fact]
    public void Placed_WhenAnotherKeyIsPlacedInTheSameSection_ThenItAnswersForTheKeyItWasAskedAbout()
    {
        // Asked per key rather than per section: a fact box yielding on a neighbour's placement
        // would drop its own row while the section drew nothing for it.
        List<PropertyMetadataEntry> metadata = [Placement(), Neighbour("Databehandler", Section)];

        Dictionary<string, string?> values = new() { ["Databehandler"] = "Hemit HF" };

        Assert.False(CatalogueProperties.Placed(metadata, values, "no", Key));
        Assert.True(CatalogueProperties.Placed(metadata, values, "no", "Databehandler"));
    }
}
