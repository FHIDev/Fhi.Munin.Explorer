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
        string type = "")
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
            Type = type,
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
    public void Rows_WhenAValueIsAMultilingualEnvelope_ThenTheReaderSeesTheirOwnLanguageRatherThanTheJson()
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
}
