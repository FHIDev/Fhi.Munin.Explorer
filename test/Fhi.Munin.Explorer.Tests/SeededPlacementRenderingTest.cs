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
public class SeededPlacementRenderingTest : BunitContext
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

    /// <summary>The seven column-backed definitions a source carries, typed as Munin types them.</summary>
    private static IReadOnlyList<PropertyMetadataEntry> KildeDefinitions(string? section) =>
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
        .. KildeDefinitions(section),
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
        ShortName = "ALS",
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
    private static IReadOnlyDictionary<string, string> KildeFacts => new Dictionary<string, string>
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
        new Dictionary<string, string>(KildeFacts)
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
    private static string Body<T>(IRenderedComponent<T> cut) where T : IComponent
    {
        var page = (IElement)cut.Find(".munin-explorer-page").Clone(true);

        foreach (var hero in page.QuerySelectorAll("dl.munin-explorer-page__facts").ToList())
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
            // word for it. Statistikktype is left out of the count entirely: no fact box draws it,
            // and the heading that names it is written twice, in the nav and over the section.
            [CatalogueColumns.Frequency] = "manedlig",
        };

        facts.Remove(CatalogueColumns.StatisticsType);

        EachDrawnOnce(body, facts);
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
    public void ColumnBackedFacts_WhenAColumnIsEmpty_ThenNothingDrawsARowForIt()
    {
        // A null column has to be absent from the merged values rather than present and empty:
        // present, it draws a labelled row with nothing in it and keeps its whole section alive.
        var kilde = Kilde(Section) with
        {
            LegalBasis = null,
            DataProcessor = null,
            ValidTo = null,
        };

        var body = Body(RenderKilde(kilde));

        Assert.DoesNotContain("Lovverk", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Databehandler", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Gyldig til", body, StringComparison.Ordinal);
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

    [Fact]
    public void PersonIdentification_WhenItsPlacementArrives_ThenTheHeroReadsTheWordTheSectionReads()
    {
        // The DataType collision, in the one other field that has it: the catalogue curates a word
        // for each code and this package translates the same codes itself, so a hero resolving one
        // while the section resolves the other puts one fact on one page in two wordings.
        var cut = RenderKilde(Kilde(Section));
        var hero = HeroValue(cut, "Grad av personidentifikasjon");

        Assert.Equal("Indirekte personidentifiserbare data", hero);
        Assert.Equal(SectionValue(cut, "Grad av personidentifikasjon"), hero);
    }

    [Fact]
    public void PersonIdentification_WhenNoPlacementHasArrived_ThenTheHeroKeepsThisPackagesOwnWord()
    {
        // Nothing can draw the catalogue's word with no section to draw it in, so the hero falls
        // back rather than emptying a slot the layout declares six tracks for.
        Assert.Equal("Indirekte identifiserbar",
                     HeroValue(RenderKilde(Kilde(section: null)), "Grad av personidentifikasjon"));
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
