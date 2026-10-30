using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Gyldig fra, gyldig til and grad av personidentifikasjon name the delkilde or kilde Munin says supplied them.</summary>
public class InheritedCaptionSourceTest : ExplorerTestContext
{
    private static readonly Guid Tromso7 = Guid.NewGuid();

    private static ValueSource Delkilde(Guid? id = null) => new()
    {
        SourceLevel = "delkilde",
        SourceId = id ?? Tromso7,
        SourceName = "Tromsø7",
        SourceNameEn = "Tromsø 7",
    };

    private static readonly ValueSource Kilde = new()
    {
        SourceLevel = "kilde",
        SourceId = Guid.NewGuid(),
        SourceName = "Tromsøundersøkelsen",
    };

    private static DatasamlingDetail Inheriting() => new()
    {
        Id = Guid.NewGuid(),
        Code = "D",
        PreferredTerm = "D",
        ParentKildeId = Guid.NewGuid(),
        ParentKildeName = "Tromsøundersøkelsen",
        EffectiveValidFrom = new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero),
        EffectiveValidTo = new DateTimeOffset(2016, 12, 31, 0, 0, 0, TimeSpan.Zero),
        EffectivePersonIdentificationLevel = "indirectlyIdentifiable",
        ValidFromSource = Delkilde(),
        ValidToSource = Delkilde(),
        PersonIdentificationLevelSource = Kilde,
    };

    private static IReadOnlyDictionary<string, InheritedCaption> Captions(DatasamlingDetail datasamling,
                                                                          string language = "nb") =>
        InheritedCaptions.For(datasamling, Texts.For(language), ReaderLanguage.Of(language));

    [Fact]
    public void For_WhenValidityAndIdentificationAreInherited_ThenEachNamesTheLevelMuninSaysSuppliedIt()
    {
        var captions = Captions(Inheriting());

        var delkilde = new InheritedCaption("Arvet fra delkilden", "Tromsø7", null, "no");
        Assert.Equal(delkilde, captions[CatalogueColumns.ValidFrom]);
        Assert.Equal(delkilde, captions[CatalogueColumns.ValidTo]);
        Assert.Equal(delkilde, captions[InheritedCaptions.ValidityPeriod]);
        Assert.Equal(new InheritedCaption("Arvet fra kilden", "Tromsøundersøkelsen", null, "no"),
                     captions[CatalogueColumns.PersonIdentification]);
    }

    [Fact]
    public void For_WhenTheReaderReadsEnglish_ThenTheCaptionUsesTheEnglishNameOrMarksTheNorwegianOne()
    {
        var captions = Captions(Inheriting(), "en");

        Assert.Equal(new InheritedCaption("Inherited from the sub-source", "Tromsø 7", null, "en"),
                     captions[CatalogueColumns.ValidFrom]);
        Assert.Equal(new InheritedCaption("Inherited from the source", "Tromsøundersøkelsen", "no", "en"),
                     captions[CatalogueColumns.PersonIdentification]);
    }

    [Fact]
    public void For_WhenTheDatasamlingHoldsItsOwnValues_ThenNoneOfThemIsCaptioned()
    {
        var captions = Captions(Inheriting() with
        {
            ValidFromSource = null,
            ValidToSource = null,
            PersonIdentificationLevelSource = null,
        });

        Assert.Empty(captions.Keys.Intersect(
        [
            CatalogueColumns.ValidFrom, CatalogueColumns.ValidTo, InheritedCaptions.ValidityPeriod,
            CatalogueColumns.PersonIdentification,
        ]));
    }

    [Fact]
    public void For_WhenTheTwoEndsComeFromDifferentPlaces_ThenEachEndIsCaptionedButNotThePeriod()
    {
        var captions = Captions(Inheriting() with { ValidToSource = Kilde });

        Assert.Equal("Tromsø7", captions[CatalogueColumns.ValidFrom].Name);
        Assert.Equal("Tromsøundersøkelsen", captions[CatalogueColumns.ValidTo].Name);
        Assert.False(captions.ContainsKey(InheritedCaptions.ValidityPeriod));
    }

    [Fact]
    public void For_WhenTwoDelkilderShareAName_ThenTheirIdsKeepThePeriodUncaptioned()
    {
        var captions = Captions(Inheriting() with { ValidToSource = Delkilde(Guid.NewGuid()) });

        Assert.False(captions.ContainsKey(InheritedCaptions.ValidityPeriod));
    }

    [Fact]
    public void For_WhenTheEndsShareAnIdButNotALevel_ThenThePeriodIsNotCaptioned()
    {
        var captions = Captions(Inheriting() with { ValidToSource = Kilde with { SourceId = Tromso7 } });

        Assert.False(captions.ContainsKey(InheritedCaptions.ValidityPeriod));
    }

    [Fact]
    public void For_WhenOneEndIsOwnAndTheOtherInherited_ThenThePeriodIsNotCaptioned()
    {
        var captions = Captions(Inheriting() with { ValidFromSource = null });

        Assert.False(captions.ContainsKey(CatalogueColumns.ValidFrom));
        Assert.True(captions.ContainsKey(CatalogueColumns.ValidTo));
        Assert.False(captions.ContainsKey(InheritedCaptions.ValidityPeriod));
    }

    [Fact]
    public void For_WhenTheStartIsInheritedAndTheEndOwn_ThenThePeriodIsNotCaptioned()
    {
        var captions = Captions(Inheriting() with { ValidToSource = null });

        Assert.True(captions.ContainsKey(CatalogueColumns.ValidFrom));
        Assert.False(captions.ContainsKey(InheritedCaptions.ValidityPeriod));
    }

    [Fact]
    public void For_WhenOnlyTheEndIsKnown_ThenThePeriodTakesItsCaption()
    {
        var captions = Captions(Inheriting() with { EffectiveValidFrom = null, ValidFromSource = null });

        Assert.Equal("Tromsø7", captions[InheritedCaptions.ValidityPeriod].Name);
    }

    [Fact]
    public void For_WhenTheValidityIsOpenEnded_ThenThePeriodTakesTheCaptionOfItsStart()
    {
        var captions = Captions(Inheriting() with { EffectiveValidTo = null, ValidToSource = null });

        Assert.Equal("Tromsø7", captions[InheritedCaptions.ValidityPeriod].Name);
    }

    [Fact]
    public void For_WhenASourceComesWithoutAValue_ThenNothingIsCaptioned()
    {
        var captions = Captions(Inheriting() with
        {
            EffectiveValidFrom = null,
            EffectiveValidTo = null,
            EffectivePersonIdentificationLevel = " ",
        });

        Assert.Empty(captions);
    }

    [Fact]
    public void For_WhenTheSourceLevelIsUnknown_ThenNoCaptionGuessesOne()
    {
        var unknown = Kilde with { SourceLevel = "region" };

        var captions = Captions(Inheriting() with
        {
            ValidFromSource = unknown,
            ValidToSource = unknown,
            PersonIdentificationLevelSource = unknown,
        });

        Assert.Empty(captions);
    }

    [Fact]
    public void DatasamlingDetail_WhenAResponseLacksTheSourceKeys_ThenTheyReadAsNullAndNothingIsCaptioned()
    {
        // A detail output-cached before Munin sent the keys lacks them entirely.
        var datasamling = JsonSerializer.Deserialize<DatasamlingDetail>(
            """
            {
              "id": "eee1a806-0345-4186-b5cf-767a0166de70", "code": "D", "preferredTerm": "D",
              "effectiveGyldigFra": "2015-01-01T00:00:00Z", "effectiveGradAvPersonidentifikasjon": "anonymous",
              "parentKildeId": "e9a6d05d-f96c-47ac-a51b-8998e063335a", "parentKildeNavn": "NorArtritt"
            }
            """, MuninExplorerClient.Json)!;

        Assert.Null(datasamling.ValidFromSource);
        Assert.Null(datasamling.PersonIdentificationLevelSource);
        Assert.Empty(Captions(datasamling));
    }

    [Fact]
    public void DatasamlingDetail_WhenMuninSendsTheSources_ThenEveryFieldLandsUnderItsWireName()
    {
        // Munin's wire shape, read with unmapped members refused.
        var strict = new JsonSerializerOptions(MuninExplorerClient.Json)
        {
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        };

        var datasamling = JsonSerializer.Deserialize<DatasamlingDetail>(
            """
            {
              "id": "eee1a806-0345-4186-b5cf-767a0166de70", "code": "D", "preferredTerm": "D",
              "parentKildeId": "e9a6d05d-f96c-47ac-a51b-8998e063335a", "parentKildeNavn": "NorArtritt",
              "gyldigFraSource": null,
              "gyldigTilSource": { "sourceLevel": "delkilde", "sourceId": "0c027ce6-2994-45bf-99ec-facad3d1703e",
                                   "sourceName": "Bølge 2", "sourceNameEn": "Wave 2" },
              "gradAvPersonidentifikasjonSource": { "sourceLevel": "kilde", "sourceId": "e9a6d05d-f96c-47ac-a51b-8998e063335a",
                                                    "sourceName": "NorArtritt", "sourceNameEn": null }
            }
            """, strict)!;

        Assert.Null(datasamling.ValidFromSource);
        Assert.Equal(new ValueSource
        {
            SourceLevel = "delkilde",
            SourceId = Guid.Parse("0c027ce6-2994-45bf-99ec-facad3d1703e"),
            SourceName = "Bølge 2",
            SourceNameEn = "Wave 2",
        }, datasamling.ValidToSource);
        Assert.Equal("kilde", datasamling.PersonIdentificationLevelSource!.SourceLevel);
        Assert.Equal("NorArtritt", datasamling.PersonIdentificationLevelSource.SourceName);
    }

    [Fact]
    public void SourceInformation_WhenNothingIsPlaced_ThenThePeriodAndTheIdentificationRowsNameTheirSource()
    {
        var box = SourceInformation(Render(Inheriting()));

        Assert.Equal("Arvet fra delkilden Tromsø7", Caption(box, "Gyldighet"));
        Assert.Equal("Arvet fra kilden Tromsøundersøkelsen", Caption(box, "Grad av personidentifikasjon"));
    }

    [Fact]
    public void SourceInformation_WhenTheEndsComeFromDifferentPlaces_ThenThePeriodRowHasNoCaption()
    {
        var box = SourceInformation(Render(Inheriting() with { ValidToSource = Kilde }));

        Assert.Null(Row(box, "Gyldighet").QuerySelector(".caption"));
    }

    [Fact]
    public void Placement_WhenACatalogueSectionDrawsTheFields_ThenTheirRowsNameTheirSource()
    {
        var cut = Render(Inheriting() with
        {
            Sections = [new SectionPlacement { Key = "datakilde", SortOrder = 1, Translations = new Dictionary<string, string> { ["no"] = "Datakilde" } }],
            PropertyMetadata =
            [
                Definition(CatalogueColumns.ValidFrom, "Gyldig fra", "Date", 1),
                Definition(CatalogueColumns.ValidTo, "Gyldig til", "Date", 2),
                Definition(CatalogueColumns.PersonIdentification, "Grad av personidentifikasjon", "SingleSelect", 3,
                           """[{"value":"indirectlyIdentifiable","label":"Indirekte personidentifiserbar"}]"""),
            ],
        });

        var rows = cut.FindAll("dl > div");
        string? CaptionOf(string label) =>
            rows.Single(row => row.QuerySelector("dt")?.TextContent == label).QuerySelector("dd p.caption")?.TextContent;

        Assert.Equal("Arvet fra delkilden Tromsø7", CaptionOf("Gyldig fra"));
        Assert.Equal("Arvet fra delkilden Tromsø7", CaptionOf("Gyldig til"));
        Assert.Equal("Arvet fra kilden Tromsøundersøkelsen", CaptionOf("Grad av personidentifikasjon"));
    }

    [Theory]
    [InlineData(CatalogueColumns.ValidTo, "Gyldig til", "Gyldig fra", "Arvet fra delkilden Tromsø7")]
    [InlineData(CatalogueColumns.ValidFrom, "Gyldig fra", "Gyldig til", "Arvet fra kilden Tromsøundersøkelsen")]
    public void Placement_WhenASectionDrawsOneEnd_ThenTheOtherEndsOwnRowIsCaptionedToo(
        string placedKey, string placedLabel, string unplacedLabel, string caption)
    {
        var cut = Render(Inheriting() with
        {
            ValidToSource = Kilde,
            Sections = [new SectionPlacement { Key = "datakilde", SortOrder = 1, Translations = new Dictionary<string, string> { ["no"] = "Datakilde" } }],
            PropertyMetadata = [Definition(placedKey, placedLabel, "Date", 1)],
        });

        var rows = cut.FindAll("dl > div");
        Assert.Equal(caption, rows.Single(row => row.QuerySelector("dt")?.TextContent == unplacedLabel)
                         .QuerySelector("dd p.caption")?.TextContent);
    }

    private static PropertyMetadataEntry Definition(string key, string label, string type, int sortOrder,
                                                    string? optionsJson = null)
    {
        return new PropertyMetadataEntry
        {
            Key = key,
            SortOrder = sortOrder,
            Type = type,
            OptionsJson = optionsJson,
            GroupKey = "datakilde",
            GroupSortOrder = 1,
            DisplayNameTranslations = new Dictionary<string, string> { ["no"] = label },
            GroupTranslations = new Dictionary<string, string> { ["no"] = "Datakilde" },
        };
    }

    private IRenderedComponent<DatasamlingView> Render(DatasamlingDetail datasamling) =>
        Render<DatasamlingView>(b => b.Add(c => c.Datasamling, datasamling));

    private static IElement SourceInformation(IRenderedComponent<DatasamlingView> cut) =>
        cut.Find(".munin-explorer-datasamling__main").Children
           .Single(e => e.QuerySelector("h3, h4, h5, h6")?.TextContent == "Kildeinformasjon")
           .QuerySelector("dl")!;

    private static IElement Row(IElement list, string label) =>
        list.QuerySelectorAll("div").Single(row => row.QuerySelector("dt")?.TextContent == label);

    private static string? Caption(IElement list, string label) =>
        Row(list, label).QuerySelector("dd p.caption")?.TextContent;
}
