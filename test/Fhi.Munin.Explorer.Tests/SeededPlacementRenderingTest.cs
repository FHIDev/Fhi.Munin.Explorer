using System.Collections.ObjectModel;
using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The column-backed facts, counted: each of them has to reach a rendered detail page exactly once,
/// whether the catalogue has placed its key in a section or has not.
/// </summary>
/// <remarks>
/// The render-side half of Munin's own
/// <c>SeededPlacementRenderingTests.ColumnBackedFactsThatGainedAPlacement_HaveExactlyOneSourceInThePayload</c>,
/// which asks the same question of the payload. Both halves are needed because the two failures are
/// opposite and neither is visible: a fact box removed before its section can draw the value blanks
/// a public field, and a section given the value while the box still draws it renders the same fact
/// twice, under one label in two different words, which reads as two legitimate rows
/// (Fhi.Metadata-bct95).
/// <para>
/// Counted outside the hero strip. That row is a summary and repeats on purpose — see
/// <see cref="DetailFacts"/> — so counting it in would make every one of these two by construction
/// and say nothing about the duplication this exists to catch. What holds the hero honest is the
/// separate assertion that it spells a fact the way the section below it does.
/// </para>
/// </remarks>
public class SeededPlacementRenderingTest : ExplorerTestContext
{
    public SeededPlacementRenderingTest() =>
        Services.AddSingleton<IMuninExplorerClient>(new HierarchyClient());

    /// <summary>The kilde view fetches its tree on its own; an empty one is enough to render.</summary>
    private sealed class HierarchyClient : EmptyMuninExplorerClient
    {
        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(
            Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(new() { KildeId = id });
    }

    /// <summary>The section the seed puts these keys in. One is enough: the count is per value.</summary>
    private const string Section = "Om kilden";

    // Munin types this one SingleSelect and curates a word for every code, and that word is not the
    // word Texts.PersonIdentificationLabel has — which is the whole reason it can be drawn twice.
    private const string IdentificationOptions =
        """
        [{"value":"indirectlyIdentifiable","label":"Indirekte personidentifiserbare data",
          "labelEn":"Indirectly identifiable data"}]
        """;

    private const string StatisticsOptions =
        """[{"value":"hendelsestelling","label":"Telling av hendelser","labelEn":"Event count"}]""";

    private const string FrequencyOptions =
        """[{"value":"manedlig","label":"Hver maaned","labelEn":"Monthly"}]""";

    /// <summary>
    /// One property definition, in the section the seed placed it in — or in none at all, which is
    /// where Munin leaves a column-backed property until the placements reach that environment.
    /// </summary>
    private static PropertyMetadataEntry Definition(
        string key, string label, string type, int sortOrder, string? section, string? optionsJson = null) =>
        new()
        {
            Key = key,
            SortOrder = sortOrder,
            Type = type,
            OptionsJson = optionsJson,
            DisplayNameTranslations = new Dictionary<string, string> { ["no"] = label },
            GroupTranslations = section is null
                ? ReadOnlyDictionary<string, string>.Empty
                : new Dictionary<string, string> { ["no"] = section },
        };

    /// <summary>The column-backed definitions a source carries, typed as Munin types them.</summary>
    private static IReadOnlyList<PropertyMetadataEntry> KildeDefinitions(string? section) =>
    [
        .. SharedDefinitions(section),
        // A collection has these too, but its page spells the parent's name and type as well, so
        // the source is where each can be counted to one (Fhi.Metadata-zg89n).
        Definition(CatalogueColumns.PreferredTerm, "Navn", "String", 10, section),
        Definition(CatalogueColumns.Code, "Kode", "String", 15, section),
        Definition(CatalogueColumns.ShortName, "Kortnavn", "String", 20, section),
        Definition(CatalogueColumns.Kildetype, "Kildetype", "SingleSelect", 25, section),
    ];

    /// <summary>The seven a source and a collection share and each page can count.</summary>
    private static IReadOnlyList<PropertyMetadataEntry> SharedDefinitions(string? section) =>
    [
        Definition(CatalogueColumns.Description, "Beskrivelse", "Text", 30, section),
        Definition(CatalogueColumns.PersonIdentification, "Grad av personidentifikasjon",
                   "SingleSelect", 70, section, IdentificationOptions),
        Definition(CatalogueColumns.LegalBasis, "Lovverk", "Url", 80, section),
        Definition(CatalogueColumns.DataController, "Dataansvarlig", "String", 90, section),
        Definition(CatalogueColumns.DataProcessor, "Databehandler", "String", 100, section),
        Definition(CatalogueColumns.ValidFrom, "Gyldig fra", "Date", 110, section),
        Definition(CatalogueColumns.ValidTo, "Gyldig til", "Date", 120, section),
    ];

    /// <summary>Those seven again, with the three a collection has and a source does not.</summary>
    private static IReadOnlyList<PropertyMetadataEntry> DatasamlingDefinitions(string? section) =>
    [
        .. SharedDefinitions(section),
        Definition(CatalogueColumns.StatisticsType, "Statistikktype", "SingleSelect", 130, section,
                   StatisticsOptions),
        Definition(CatalogueColumns.CountingUnit, "Telleenhet", "String", 140, section),
        Definition(CatalogueColumns.Frequency, "Frekvens", "SingleSelect", 150, section, FrequencyOptions),
    ];

    /// <summary>
    /// A source with every column-backed field filled in, which is the only state that can show a
    /// value drawn twice — an empty column draws nothing however many renderers want it.
    /// </summary>
    private static KildeDetail Kilde(string? section) => new()
    {
        Id = Guid.NewGuid(),
        Code = "K_ALS",
        // Not "ALS", which the code already contains: a count of it would find two.
        ShortName = "ALSR",
        PreferredTerm = "Als registeret",
        Description = "Norsk register for motonevronsykdommer.",
        Kildetype = "nasjonaltMedisinskKvalitetsregister",
        // Prose rather than an address although the catalogue types this Url: a link would put the
        // same text in an href as well, and this test counts text.
        LegalBasis = "Forskrift om medisinske kvalitetsregistre § 2-3.",
        DataController = "St. Olavs hospital HF",
        DataProcessor = "Hemit HF",
        PersonIdentificationLevel = "indirectlyIdentifiable",
        ValidFrom = new DateTimeOffset(2023, 2, 3, 0, 0, 0, TimeSpan.Zero),
        ValidTo = new DateTimeOffset(2024, 4, 5, 0, 0, 0, TimeSpan.Zero),
        LastUpdated = new DateTimeOffset(2026, 3, 4, 9, 30, 0, TimeSpan.Zero),
        DataFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TotalVariables = 312,
        PropertyMetadata = KildeDefinitions(section),
    };

    /// <inheritdoc cref="Kilde"/>
    private static DatasamlingDetail Datasamling(string? section) => new()
    {
        Id = Guid.NewGuid(),
        Code = "D_ALS.INKL",
        PreferredTerm = "Inklusjon",
        Description = "Skjemaet som melder en pasient inn i registeret.",
        ParentKildeId = Guid.NewGuid(),
        ParentKildeName = "Als registeret",
        EffectiveKildetype = "nasjonaltMedisinskKvalitetsregister",
        EffectiveLegalBasis = "Forskrift om medisinske kvalitetsregistre § 2-3.",
        EffectiveDataController = "St. Olavs hospital HF",
        EffectiveDataProcessor = "Hemit HF",
        EffectivePersonIdentificationLevel = "indirectlyIdentifiable",
        EffectiveValidFrom = new DateTimeOffset(2023, 2, 3, 0, 0, 0, TimeSpan.Zero),
        EffectiveValidTo = new DateTimeOffset(2024, 4, 5, 0, 0, 0, TimeSpan.Zero),
        StatisticsType = "hendelsestelling",
        CountingUnit = "Innmeldt pasient",
        Frequency = "manedlig",
        LastUpdated = new DateTimeOffset(2026, 3, 4, 9, 30, 0, TimeSpan.Zero),
        VariableCount = 42,
        PropertyMetadata = DatasamlingDefinitions(section),
    };

    /// <summary>A variable, whose one column-backed value the catalogue has a key for.</summary>
    private static VariableDetail Variable(string? section) => new()
    {
        Id = Guid.NewGuid(),
        Code = "V_ALS.F1.TALE",
        PreferredTerm = "1. Tale",
        Description = "Skalaen maaler taleevne.",
        KildeName = "Als registeret",
        KildeShortName = "ALS",
        KildeType = "nasjonaltMedisinskKvalitetsregister",
        PropertyMetadata = [Definition(CatalogueColumns.Description, "Beskrivelse", "Text", 30, section)],
    };

    /// <summary>What each key has to be readable as, exactly once, when the section draws it.</summary>
    private static IReadOnlyDictionary<string, string> KildeFacts => new Dictionary<string, string>(SharedFacts)
    {
        [CatalogueColumns.PreferredTerm] = "Als registeret",
        [CatalogueColumns.Code] = "K_ALS",
        [CatalogueColumns.ShortName] = "ALSR",
        [CatalogueColumns.Kildetype] = "Nasjonalt medisinsk kvalitetsregister",
    };

    /// <inheritdoc cref="KildeFacts"/>
    private static IReadOnlyDictionary<string, string> SharedFacts => new Dictionary<string, string>
    {
        [CatalogueColumns.Description] = "Norsk register for motonevronsykdommer.",
        [CatalogueColumns.PersonIdentification] = "Indirekte personidentifiserbare data",
        [CatalogueColumns.LegalBasis] = "Forskrift om medisinske kvalitetsregistre § 2-3.",
        [CatalogueColumns.DataController] = "St. Olavs hospital HF",
        [CatalogueColumns.DataProcessor] = "Hemit HF",
        [CatalogueColumns.ValidFrom] = "3. februar 2023",
        [CatalogueColumns.ValidTo] = "5. april 2024",
    };

    /// <inheritdoc cref="KildeFacts"/>
    private static IReadOnlyDictionary<string, string> DatasamlingFacts =>
        new Dictionary<string, string>(SharedFacts)
        {
            [CatalogueColumns.Description] = "Skjemaet som melder en pasient inn i registeret.",
            [CatalogueColumns.StatisticsType] = "Telling av hendelser",
            [CatalogueColumns.CountingUnit] = "Innmeldt pasient",
            [CatalogueColumns.Frequency] = "Hver maaned",
        };

    /// <inheritdoc cref="KildeFacts"/>
    private static IReadOnlyDictionary<string, string> VariableFacts => new Dictionary<string, string>
    {
        [CatalogueColumns.Description] = "Skalaen maaler taleevne.",
    };

    /// <summary>
    /// The page's text with the hero strip taken out, which is what a count is taken over.
    /// </summary>
    /// <remarks>
    /// The sticky bar goes with it: it repeats the first few of those same facts, so a page that
    /// kept it would count each of them twice and every claim below would be about the repeat.
    /// </remarks>
    private static string Body<T>(IRenderedComponent<T> cut) where T : IComponent
    {
        var page = (IElement)cut.Find(".munin-explorer-page").Clone(true);

        foreach (var hero in page
                     .QuerySelectorAll("dl.munin-explorer-page__facts, .munin-explorer-page__stuckbar")
                     .ToList())
        {
            hero.Remove();
        }

        return page.TextContent;
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;

        for (var at = text.IndexOf(value, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>Every fact in the table, counted at once, so a failure names the one that moved.</summary>
    private static void EachDrawnOnce(string body, IReadOnlyDictionary<string, string> facts)
    {
        foreach (var (key, value) in facts)
        {
            Assert.Equal((key, 1), (key, Occurrences(body, value)));
        }
    }

    private IRenderedComponent<KildeView> RenderKilde(KildeDetail kilde) =>
        Render<KildeView>(b => b.Add(c => c.Kilde, kilde));

    private IRenderedComponent<DatasamlingView> RenderDatasamling(DatasamlingDetail datasamling) =>
        Render<DatasamlingView>(b => b.Add(c => c.Datasamling, datasamling));

    private IRenderedComponent<VariableView> RenderVariable(VariableDetail variable) =>
        Render<VariableView>(b => b.Add(c => c.Variable, variable));

    [Fact]
    public void ColumnBackedFacts_WhenThePlacementsHaveArrived_ThenTheKildePageDrawsEachExactlyOnce()
    {
        // The half that fails when a fact box is left in place: the section now has the value, so
        // every row the box still draws is a second copy of it.
        EachDrawnOnce(Body(RenderKilde(Kilde(Section))), KildeFacts);
    }

    [Fact]
    public void ColumnBackedFacts_WhenNoPlacementHasArrived_ThenTheKildePageStillDrawsEachExactlyOnce()
    {
        // The half that fails when a fact box is removed outright. Munin places these keys per
        // environment, so a page reading an API without the seed has only the box, and a row taken
        // out unconditionally is a blank field on a public page.
        var body = Body(RenderKilde(Kilde(section: null)));

        EachDrawnOnce(body, new Dictionary<string, string>(KildeFacts)
        {
            // This package's own vocabulary, which is what there is to say until the catalogue's
            // word can be drawn.
            [CatalogueColumns.PersonIdentification] = "Indirekte identifiserbar",
            // Two dates as one period, which is the only shape a fact box has for them.
            [CatalogueColumns.ValidFrom] = "3. februar 2023 – 5. april 2024",
            [CatalogueColumns.ValidTo] = "3. februar 2023 – 5. april 2024",
        });
    }

    [Fact]
    public void ColumnBackedFacts_WhenThePlacementsHaveArrived_ThenTheBoxesLeaveThoseRowsOutRatherThanSayingNone()
    {
        // A row with no value now reads "Ingen", so a fact that yields to a section has to take its
        // row with it: "drawn in another section" is not "the catalogue holds nothing". Counting
        // values cannot see this, since "Ingen" is not the value. (Fhi.Metadata-35w0p.24)
        string[] placed = ["Lovverk", "Dataansvarlig", "Databehandler", "Grad av personidentifikasjon"];

        var kilde = RenderKilde(Kilde(Section));
        var datasamling = RenderDatasamling(Datasamling(Section));

        // The boxes are drawn, so the DoesNotContain below cannot pass on a box that is gone.
        Assert.Contains("Type datakilde", BoxLabels(kilde, DetailSectionIds.Source));
        Assert.Contains("Type datakilde", BoxLabels(datasamling, DetailSectionIds.Source));
        Assert.Contains("Antall variabler", BoxLabels(datasamling, DetailSectionIds.Statistics));

        Assert.All(placed, label => Assert.DoesNotContain(label, BoxLabels(kilde, DetailSectionIds.Source)));
        Assert.All(placed, label => Assert.DoesNotContain(label, BoxLabels(datasamling, DetailSectionIds.Source)));
        Assert.All(["Statistikktype", "Frekvens", "Telleenhet"],
                   label => Assert.DoesNotContain(label, BoxLabels(datasamling, DetailSectionIds.Statistics)));
    }

    /// <summary>Scoped to the box's own section, so a label a curated section draws does not count as the box's.</summary>
    private static IReadOnlyList<string> BoxLabels<T>(IRenderedComponent<T> cut, string sectionId) where T : IComponent =>
        [.. cut.FindAll($"section#{sectionId} dl.munin-explorer-page__fields dt").Select(dt => dt.TextContent)];

    [Fact]
    public void ColumnBackedFacts_WhenThePlacementsHaveArrived_ThenTheDatasamlingPageDrawsEachExactlyOnce()
    {
        // The three a collection has beyond a source's seven are the point of doing it here as
        // well: Frekvens and Telleenhet leave the Statistikk box, and both are drawn by nothing
        // else on the page.
        EachDrawnOnce(Body(RenderDatasamling(Datasamling(Section))), DatasamlingFacts);
    }

    [Fact]
    public void ColumnBackedFacts_WhenNoPlacementHasArrived_ThenTheDatasamlingPageStillDrawsEachExactlyOnce()
    {
        var body = Body(RenderDatasamling(Datasamling(section: null)));

        var facts = new Dictionary<string, string>(DatasamlingFacts)
        {
            [CatalogueColumns.PersonIdentification] = "Indirekte identifiserbar",
            [CatalogueColumns.ValidFrom] = "3. februar 2023 – 5. april 2024",
            [CatalogueColumns.ValidTo] = "3. februar 2023 – 5. april 2024",
            // Unresolved, because the fact box draws the stored code rather than the vocabulary's
            // word for it. Statistikktype is left out of the count entirely: the heading, the nav
            // entry and the row all read it off one field, so exactly-once is the wrong question.
            [CatalogueColumns.Frequency] = "manedlig",
        };

        facts.Remove(CatalogueColumns.StatisticsType);

        EachDrawnOnce(body, facts);
    }

    [Fact]
    public void Statistics_WhenTheTypeIsPlacedAndNothingElseFillsTheBlock_ThenNoHeadingIsLeftOverNothing()
    {
        // Statistikktype is the one merged key whose heading does not yield — it names the section
        // rather than repeating the fact — so heading and rows can disagree here and nowhere else.
        // Gated on the heading this would be an empty section with a nav entry pointing into it.
        var cut = RenderDatasamling(Datasamling(Section) with
        {
            Frequency = null,
            CountingUnit = null,
            VariableCount = 0,
        });

        Assert.Empty(cut.FindAll($"section#{DetailSectionIds.Statistics}"));
        Assert.DoesNotContain("#" + DetailSectionIds.Statistics,
                              cut.FindAll(".munin-explorer-page__toc a")
                                 .Select(link => link.GetAttribute("href")!));

        // Still on the page once, in the section the catalogue placed it in and in its words.
        Assert.Equal("Telling av hendelser", SectionValue(cut, "Statistikktype"));
        Assert.Equal(1, Occurrences(Body(cut), "Telling av hendelser"));
    }

    [Fact]
    public void Description_WhenItsPlacementArrives_ThenTheIngressIsStillTheOnlyPlaceEachPageDrawsIt()
    {
        // Beskrivelse is the one key of the ten that three different views already draw in their
        // name block, so it is the one that joins drawnElsewhere rather than leaving a box.
        Assert.Equal(1, Occurrences(Body(RenderKilde(Kilde(Section))),
                                    KildeFacts[CatalogueColumns.Description]));
        Assert.Equal(1, Occurrences(Body(RenderDatasamling(Datasamling(Section))),
                                    DatasamlingFacts[CatalogueColumns.Description]));
        EachDrawnOnce(Body(RenderVariable(Variable(Section))), VariableFacts);
    }

    [Fact]
    public void Description_WhenTheCollectionOnlyRepeatsItsName_ThenTheSectionDrawsItRatherThanNobody()
    {
        // The ingress is suppressed for a description that only restates the heading, so the key
        // must not be suppressed from the section as well — that would be the blank field again,
        // reached by the one route a flat drawnElsewhere set cannot see.
        var datasamling = Datasamling(Section) with { Description = "Inklusjon" };

        Assert.Equal("Inklusjon", SectionValue(RenderDatasamling(datasamling), "Beskrivelse"));
    }

    [Fact]
    public void ColumnBackedFacts_WhenAColumnIsEmpty_ThenOnlyTheFactBoxDrawsARowAndItSaysSo()
    {
        // A null column has to be absent from the merged values rather than present and empty:
        // present, the section draws a labelled row with nothing in it beside the fact box's own.
        // The fact box keeps the row, reading "Ingen" (Fhi.Metadata-35w0p.24).
        var kilde = Kilde(Section) with
        {
            LegalBasis = null,
            DataProcessor = null,
            ValidTo = null,
        };

        var cut = RenderKilde(kilde);
        var body = Body(cut);

        Assert.Equal(1, Occurrences(body, "Lovverk"));
        Assert.Equal(1, Occurrences(body, "Databehandler"));
        Assert.Equal("Ingen", SectionValue(cut, "Lovverk"));
        Assert.Equal("Ingen", SectionValue(cut, "Databehandler"));

        // An open end after a start the section draws is ongoing, as a whole period would say.
        Assert.Equal(1, Occurrences(body, "Gyldig til"));
        Assert.Equal("Pågående", SectionValue(cut, "Gyldig til"));
        Assert.Contains("Gyldig fra", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CatalogueColumns.ValidFrom)]
    [InlineData(CatalogueColumns.ValidTo)]
    public void Validity_WhenOnlyOneEndIsPlaced_ThenNeitherPageLeavesTheOtherEndUndrawn(string placed)
    {
        // Placements are per key and are editable master data, so one end can be placed while the
        // other is not. A fact box that moved its whole period on either placement would take the
        // unplaced date off the page altogether, which is this bead's blank field.
        PropertyMetadataEntry[] placement =
        [
            Definition(CatalogueColumns.ValidFrom, "Gyldig fra", "Date", 110,
                       placed == CatalogueColumns.ValidFrom ? Section : null),
            Definition(CatalogueColumns.ValidTo, "Gyldig til", "Date", 120,
                       placed == CatalogueColumns.ValidTo ? Section : null),
        ];

        string[] pages =
        [
            Body(RenderKilde(Kilde(section: null) with { PropertyMetadata = placement })),
            Body(RenderDatasamling(Datasamling(section: null) with { PropertyMetadata = placement })),
        ];

        foreach (var body in pages)
        {
            Assert.Equal(1, Occurrences(body, KildeFacts[CatalogueColumns.ValidFrom]));
            Assert.Equal(1, Occurrences(body, KildeFacts[CatalogueColumns.ValidTo]));
        }
    }

    /// <summary>The two validity definitions, each placed in a section or left in none.</summary>
    private static PropertyMetadataEntry[] ValidityPlacement(bool from, bool to) =>
    [
        Definition(CatalogueColumns.ValidFrom, "Gyldig fra", "Date", 110, from ? Section : null),
        Definition(CatalogueColumns.ValidTo, "Gyldig til", "Date", 120, to ? Section : null),
    ];

    /// <summary>
    /// Every validity row the two pages' own fact boxes draw, for one placement of the pair.
    /// </summary>
    /// <remarks>
    /// The box alone, scoped by section id, because which surface draws the validity is the thing
    /// under test — and label and value together, because a count over the date text cannot tell
    /// "the From row is drawn" from "the To row is drawn" when the two ends format alike.
    /// </remarks>
    private IReadOnlyList<(string Page, string Label, string Value)> ValidityRows(bool from, bool to)
    {
        var placement = ValidityPlacement(from, to);

        return
        [
            .. Validity("kilde", RenderKilde(Kilde(section: null) with { PropertyMetadata = placement })),
            .. Validity("datasamling",
                        RenderDatasamling(Datasamling(section: null) with { PropertyMetadata = placement })),
        ];
    }

    /// <inheritdoc cref="ValidityRows"/>
    private static IEnumerable<(string Page, string Label, string Value)> Validity<T>(
        string page, IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll($"#{DetailSectionIds.Source} dl.munin-explorer-page__fields div")
           .Select(row => (page, row.QuerySelector("dt")!.TextContent, row.QuerySelector("dd")!.TextContent))
           .Where(row => row.Item2 is "Gyldighet" or "Gyldig fra" or "Gyldig til");

    private const string Period = "3. februar 2023 – 5. april 2024";

    [Fact]
    public void Validity_WhenTheStartIsHeldOnlyInTheCuratedValues_ThenTheOpenEndStillReadsOngoing()
    {
        // The bag wins the merge, so a section can draw a start the typed column does not hold.
        // Reading "ongoing" off the typed start would call that open end "Ingen". (Fhi.Metadata-35w0p.24)
        var kilde = Kilde(section: null) with
        {
            PropertyMetadata = ValidityPlacement(from: true, to: true),
            ValidFrom = null,
            ValidTo = null,
            AdditionalProperties = new Dictionary<string, string?> { [CatalogueColumns.ValidFrom] = "2023-02-03" },
        };

        Assert.Equal([("kilde", "Gyldig til", "Pågående")], [.. Validity("kilde", RenderKilde(kilde))]);
    }

    [Fact]
    public void Validity_WhenNeitherEndIsPlaced_ThenBothFactBoxesDrawThePeriodUnderTheValidityLabel()
    {
        // The branch every page takes until the placements arrive, and the only one that draws both
        // ends in one row.
        Assert.Equal([("kilde", "Gyldighet", Period), ("datasamling", "Gyldighet", Period)],
                     ValidityRows(from: false, to: false));
    }

    [Fact]
    public void Validity_WhenOnlyGyldigFraIsPlaced_ThenBothFactBoxesDrawTheClosingEndAlone()
    {
        // The section has taken the opening date, so the box keeps the one it has not — under the
        // closing label, which is a label this box never used before the placements existed.
        Assert.Equal([("kilde", "Gyldig til", "5. april 2024"),
                      ("datasamling", "Gyldig til", "5. april 2024")],
                     ValidityRows(from: true, to: false));
    }

    [Fact]
    public void Validity_WhenOnlyGyldigTilIsPlaced_ThenBothFactBoxesDrawTheOpeningEndAlone()
    {
        // The mirror of it, and the branch a swap would leave rendering plausible-looking output:
        // the period beside a placed closing end reads as ongoing, and the wrong end repeats a date
        // the section is already drawing.
        Assert.Equal([("kilde", "Gyldig fra", "3. februar 2023"),
                      ("datasamling", "Gyldig fra", "3. februar 2023")],
                     ValidityRows(from: false, to: true));
    }

    [Fact]
    public void Validity_WhenBothEndsArePlaced_ThenNeitherFactBoxDrawsAValidityRowAtAll()
    {
        // The branch that silently removes a row from a public fact box. Safe only because the two
        // sections are now drawing both dates, which the count assertions above establish.
        Assert.Empty(ValidityRows(from: true, to: true));
    }

    private const string IdentificationLabel = "Grad av personidentifikasjon";

    /// <summary>
    /// The word each page's hero draws for the identification level, beside the word the row below
    /// it draws — the catalogue's section once the key is placed, the view's own fact box until
    /// then.
    /// </summary>
    /// <remarks>
    /// Both pages because each resolves the level in a member of its own, so an assertion on one
    /// says nothing about the other's branch.
    /// </remarks>
    private IReadOnlyList<(string Page, string Hero, string Row)> Identification(string? section)
    {
        var kilde = RenderKilde(Kilde(section));
        var datasamling = RenderDatasamling(Datasamling(section));

        return
        [
            ("kilde", HeroValue(kilde, IdentificationLabel), SectionValue(kilde, IdentificationLabel)),
            ("datasamling", HeroValue(datasamling, "Personidentifikasjon"),
             SectionValue(datasamling, IdentificationLabel)),
        ];
    }

    [Fact]
    public void PersonIdentification_WhenItsPlacementArrives_ThenTheHeroReadsTheWordTheSectionReads()
    {
        // The DataType collision, in the one other field that has it: the catalogue curates a word
        // for each code and this package translates the same codes itself, so a hero resolving one
        // while the section resolves the other puts one fact on one page in two wordings.
        const string Curated = "Indirekte personidentifiserbare data";

        Assert.Equal([("kilde", Curated, Curated), ("datasamling", Curated, Curated)],
                     Identification(Section));
    }

    [Fact]
    public void PersonIdentification_WhenNoPlacementHasArrived_ThenTheHeroKeepsThisPackagesOwnWord()
    {
        // Nothing can draw the catalogue's word with no section to draw it in, so both surfaces
        // fall back rather than emptying a slot the layout declares six tracks for.
        const string Own = "Indirekte identifiserbar";

        Assert.Equal([("kilde", Own, Own), ("datasamling", Own, Own)],
                     Identification(section: null));
    }

    /// <summary>One hero cell's value — the summary strip the counts above deliberately leave out.</summary>
    private static string HeroValue<T>(IRenderedComponent<T> cut, string label) where T : IComponent =>
        Value(cut.FindAll("dl.munin-explorer-page__facts div"), label);

    [Fact]
    public void ColumnKeys_Always_ThenEveryOneCatalogueColumnsCanMergeIsCountedBySomeSurface()
    {
        // Derived rather than listed a second time: a key added to CatalogueColumns and to nobody's
        // expectations would be merged into the renderable set and never counted. Asked per surface
        // because the datasamling table is a copy of the kilde's — against the union of them, a key
        // added to that copy alone would pass while the other two surfaces drew it uncounted.
        (string Surface, IReadOnlyList<string> Defined, IEnumerable<string> Counted)[] surfaces =
        [
            ("kilde", Keys(KildeDefinitions(Section)), KildeFacts.Keys),
            ("datasamling", Keys(DatasamlingDefinitions(Section)), DatasamlingFacts.Keys),
            ("variabel", Keys(Variable(Section).PropertyMetadata), VariableFacts.Keys),
        ];

        foreach (var (surface, defined, counted) in surfaces)
        {
            Assert.Equal((surface, ""), (surface, Missing(defined, counted)));
        }

        Assert.Equal("", Missing(MergeableKeys, surfaces.SelectMany(surface => surface.Defined)));
    }

    /// <summary>Every key CatalogueColumns can merge into the renderable set, read off the class.</summary>
    private static IReadOnlyList<string> MergeableKeys =>
        [.. typeof(CatalogueColumns)
            .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(member => member.IsLiteral && member.FieldType == typeof(string))
            .Select(member => (string)member.GetRawConstantValue()!)];

    private static IReadOnlyList<string> Keys(IEnumerable<PropertyMetadataEntry> definitions) =>
        [.. definitions.Select(entry => entry.Key)];

    /// <summary>The keys the second side does not carry, named so a failure says which they are.</summary>
    private static string Missing(IEnumerable<string> keys, IEnumerable<string> carried) =>
        string.Join(", ", keys.Except(carried, StringComparer.Ordinal).Order(StringComparer.Ordinal));

    /// <summary>
    /// One row's value anywhere in the page's fact lists, found by the label beside it.
    /// </summary>
    /// <remarks>
    /// Across every list rather than inside a named one: which list a fact lands in is the thing
    /// under test, so a lookup that had to be told would be asserting its own argument. Every label
    /// asked for here is drawn once on the page, which is what the counts above establish.
    /// </remarks>
    private static string SectionValue<T>(IRenderedComponent<T> cut, string label) where T : IComponent =>
        Value(cut.FindAll("dl.munin-explorer-page__fields div"), label);

    /// <summary>One row's value, found by the label beside it rather than by its position.</summary>
    private static string Value(IEnumerable<IElement> rows, string label) =>
        rows.FirstOrDefault(row => row.QuerySelector("dt")?.TextContent == label)
            ?.QuerySelector("dd")?.TextContent
        ?? throw new InvalidOperationException($"No '{label}' row is drawn on this page.");
}
