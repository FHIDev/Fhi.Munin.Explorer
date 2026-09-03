using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The whole kilde: the view both explorers render a source with — its name block, the catalogue's
/// own metadata, the datasamlinger it holds and the two sidebar boxes.
/// </summary>
/// <remarks>
/// Written because this component had no test class of its own. The suite had one for the explorer,
/// one for the variable view and one for the filter panel, and the kilde view was only ever reached
/// sideways, through the explorer's drill-in — so the parameters it exists for had no coverage.
/// The parameters are the point, in two different ways. <see cref="KildeView.Sections"/> and
/// <see cref="KildeView.DataCollectionsHeading"/> are the whole reason this is a shared core rather
/// than two views. Kelda wires <c>Sections</c> — <c>KildeExplorer.razor</c> hands it three sections
/// of its own — and neither explorer wires the heading any more (Fhi.Metadata-rhybi), which is why
/// both are exercised here directly: the assertions below are about what the core does with them,
/// and <c>KildeSectionsTest</c> is about the difference they make between the two explorers.
/// <see cref="KildeView.HeadingLevel"/> and <see cref="KildeView.HeadingId"/> are wired — the
/// explorer passes both at VariableSearch.razor:104-107 — and the explorer's tests already follow
/// two of the things that come out: the title's level, mounted at h1 and asserted h2
/// (<c>Source_WhenThePanelIsOpen_ThenItsHeadingSitsBelowTheCardsInTheOutline</c>,
/// VariableSearchTest.cs:5763), and the id the landmark is named by, resolved back to this view's
/// name heading (<c>Source_WhenOpened_ThenTheToggleAndTheRegionAreWiredToEachOther</c>, :5478).
/// What no test covered is narrower: the levels the blocks under the title land on, and either
/// parameter with the view rendered whole rather than reached through the drill-in.
/// <para>
/// The class-name check is the one thing here that was already hanging somewhere: the explorer's
/// own <c>Source_WhenAPanelIsOpen_ThenItIsBuiltFromShapesRatherThanFromANewStyleName</c>
/// (VariableSearchTest.cs:5701) opens the drill-in and pins nine of the ten names this view
/// emits. Both lists are worth keeping — that one guards the path a reader actually takes, this one
/// guards the view rendered whole — but they are two hand-maintained lists in two files, so a name
/// added or renamed here has to be answered in both.
/// </para>
/// </remarks>
public class KildeViewTest : BunitContext
{

    private static PropertyMetadataEntry Entry(string key, int sortOrder, string group, string? displayName = null) =>
        new()
        {
            Key = key,
            SortOrder = sortOrder,
            GroupTranslations = new Dictionary<string, string> { ["no"] = group },
            DisplayNameTranslations = new Dictionary<string, string> { ["no"] = displayName ?? key },
        };

    /// <summary>A source shaped like the ALS register, which is the one the captured payloads hold.</summary>
    private static KildeDetail Kilde() => new()
    {
        Id = Guid.NewGuid(),
        Code = "K_ALS",
        ShortName = "ALS",
        PreferredTerm = "Als registeret",
        Description = "Norsk register for ALS og andre motonevronsykdommer.",
        Kildetype = "nasjonaltMedisinskKvalitetsregister",
        LegalBasis = "Forskrift om medisinske kvalitetsregistre § 2-3.",
        // Two organisations rather than one twice: with the same string in both, a component
        // reading DataProcessor into the dataansvarlig row would satisfy every assertion here while
        // attributing the data to the wrong body.
        DataController = "St. Olavs hospital HF",
        DataProcessor = "Hemit HF",
        PersonIdentificationLevel = "indirectlyIdentifiable",
        ValidFrom = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
        LastUpdated = new DateTimeOffset(2026, 3, 4, 9, 30, 0, TimeSpan.Zero),
        DataFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TotalVariables = 312,
        PropertyMetadata =
        [
            Entry("Formaal", 10, "Formål", "Formål med registeret"),
            Entry("Kommentar", 50, "Beskrivelse"),
            // Curated but never filled in, which is the common case: five groups of a typical
            // source's thirteen go this way, so a group that draws anyway draws blank rows.
            Entry("Datakvalitet", 20, "Datakvalitet"),
        ],
        AdditionalProperties = new Dictionary<string, string?>
        {
            ["Formaal"] = "Kvalitetssikring av behandlingen.",
            ["Kommentar"] = "Gyldig fra 2019.",
        },
        Datasamlinger = [Collection("Inklusjon")],
        Delkilder = [],
    };

    private static KildeDatasamling Collection(
        string name,
        int? order = null,
        string? shortName = null,
        string description = "",
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int variables = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ShortName = shortName,
            Description = description,
            PresentationOrder = order,
            EffectiveValidFrom = from,
            EffectiveValidTo = to,
            VariableCount = variables,
        };

    private static KildeDelkilde Delkilde(
        string name,
        IReadOnlyList<KildeDatasamling> datasamlinger,
        IReadOnlyList<KildeDelkilde>? children = null,
        int? order = null,
        string code = "",
        string? shortName = null,
        string description = "") =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            ShortName = shortName,
            Description = description,
            PresentationOrder = order,
            Datasamlinger = datasamlinger,
            Children = children ?? [],
        };

    /// <summary>
    /// A source arranged the way a study series is: datasamlinger of its own, three waves beside
    /// them, and one wave with a wave of its own.
    /// </summary>
    /// <remarks>
    /// THE TRAP every claim about the structure has to be put to. Most kilder have no delkilder at
    /// all, and on those the arranged section and the flat table it replaced render the same
    /// picture — an assertion that passes on one of them has not run. The nesting goes two levels
    /// deep for the same reason one level is not enough: it cannot tell "the tree is walked" from
    /// "the top of the tree is drawn".
    /// </remarks>
    private static KildeDetail Study() => Kilde() with
    {
        Datasamlinger = [Collection("Inklusjon")],
        Delkilder =
        [
            Delkilde("Tromsø 4",
                     [Collection("Spørreskjema")],
                     [Delkilde("Første besøk", [Collection("Blodprøver")], code: "K_TR.TR4.V1",
                               description: "Første besøksrunde.")],
                     code: "K_TR.TR4",
                     // A markdown link, because the captured Tromsø payload authors this field that
                     // way and delkilde.beskrivelse carries more of them than any other field.
                     description: "Fjerde runde av [Tromsøundersøkelsen](https://uit.no/tromsoundersokelsen)."),
        ],
    };

    /// <summary>Markup a host might hang after the metadata, carrying no class of its own.</summary>
    private static readonly RenderFragment KeldaSections = builder =>
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "id", "kelda-sections");
        builder.AddContent(2, "Tilgangskriterier");
        builder.CloseElement();
    };

    private IRenderedComponent<KildeView> Render(
        KildeDetail? kilde,
        string? language = null,
        int headingLevel = 2,
        string? headingId = null,
        string? dataCollectionsHeading = null,
        RenderFragment? sections = null) =>
        Render<KildeView>(b =>
        {
            b.Add(c => c.Kilde, kilde)
             .Add(c => c.Language, language)
             .Add(c => c.HeadingLevel, headingLevel)
             .Add(c => c.HeadingId, headingId)
             .Add(c => c.DataCollectionsHeading, dataCollectionsHeading);

            // Left unset rather than set to null when no explorer passes any, which is the state a
            // host actually renders this view in.
            if (sections is not null)
            {
                b.Add(c => c.Sections, sections);
            }
        });

    /// <summary>The sidebar's first box — the facts every source has.</summary>
    private static IElement SourceInformation(IRenderedComponent<KildeView> cut) =>
        Box(cut, T => T.HeadingSourceInformation);

    /// <summary>The sidebar's second box — the counts and dates.</summary>
    private static IElement Statistics(IRenderedComponent<KildeView> cut) =>
        Box(cut, T => T.HeadingStatistics);

    /// <summary>
    /// One sidebar box, found by the heading over it rather than by its position.
    /// </summary>
    /// <remarks>
    /// A box whose every fact is blank draws no <c>dl</c> at all, while its heading is drawn
    /// unconditionally — so the boxes slide up under headings that stay put, and a position would
    /// hand back the statistics for a call asking after the source information without being able
    /// to say it had. The heading can say it: the lookup starts there, and a box that stopped
    /// drawing is reported as itself rather than as whatever the next call misreads.
    /// </remarks>
    private static IElement Box(IRenderedComponent<KildeView> cut, Func<Texts, string> boxHeading)
    {
        var aside = cut.Find(".munin-explorer-kilde__aside");
        var name = boxHeading(Texts.For(cut.Instance.Language));

        var heading = aside.Children.FirstOrDefault(e => e.TextContent == name)
                      ?? throw new InvalidOperationException(
                          $"No '{name}' heading in the sidebar, only: "
                          + $"{string.Join(", ", aside.Children.Select(e => e.TextContent))}.");

        return heading.NextElementSibling is { TagName: "DL" } box
            ? box
            : throw new InvalidOperationException(
                $"The '{name}' heading is followed by {heading.NextElementSibling?.TagName ?? "nothing"} "
                + "rather than by its own box, so that box drew no facts at all.");
    }

    private static IReadOnlyList<string> Labels(IElement list) =>
        [.. list.QuerySelectorAll("dt").Select(e => e.TextContent)];

    private static IReadOnlyList<string> Values(IElement list) =>
        [.. list.QuerySelectorAll("dd").Select(e => e.TextContent)];

    /// <summary>One row's value cell, found by the label beside it rather than by its position.</summary>
    /// <remarks>
    /// A row's index is a function of both the order the component lists its fields in and which of
    /// them the fixture filled in, since a blank value draws no row — so an index in a test that
    /// does not also assert the labels names a row that a field inserted upstream silently moves.
    /// Asking by label makes the assertion self-locating, and makes the failure say which row went
    /// missing rather than reading the wrong one's text back.
    /// </remarks>
    private static IElement Fact(IElement list, string label) =>
        list.QuerySelectorAll("div").FirstOrDefault(row => row.QuerySelector("dt")?.TextContent == label)
            ?.QuerySelector("dd")
        ?? throw new InvalidOperationException(
            $"No '{label}' row in this box, only: {string.Join(", ", Labels(list))}.");

    /// <inheritdoc cref="Fact"/>
    private static string Value(IElement list, string label) => Fact(list, label).TextContent;

    /// <summary>The datasamling rows, by the name each is headed with.</summary>
    private static IReadOnlyList<string> CollectionNames(IRenderedComponent<KildeView> cut) =>
        [.. cut.FindAll("table.munin-explorer-kilde__datasamlinger tbody th").Select(e => e.TextContent)];

    /// <summary>
    /// The datasamling section read back as an outline: a line per delkilde, in brackets, and a
    /// line per datasamling, each indented by how deeply the MARKUP nests it.
    /// </summary>
    /// <remarks>
    /// The indentation here is read off the DOM rather than off a stylesheet, which is the whole
    /// point of reading it this way. A flat list of names — which is what
    /// <see cref="CollectionNames"/> returns, and what this section used to be — satisfies every
    /// assertion about which datasamlinger are present while saying nothing about which delkilde
    /// each belongs to. So does a stack of &lt;div&gt;s indented by CSS, to an automated
    /// accessibility check as well as to a name-by-name assertion. Descending through the
    /// &lt;li&gt; is the only reading that fails when the relationship is gone.
    /// </remarks>
    private static IReadOnlyList<string> Outline(IRenderedComponent<KildeView> cut)
    {
        var lines = new List<string>();

        void Walk(IElement parent, int depth)
        {
            foreach (var child in parent.Children)
            {
                var indent = new string(' ', depth * 2);

                if (child.ClassList.Contains("munin-explorer-kilde__datasamlinger"))
                {
                    lines.AddRange(child.QuerySelectorAll("tbody th").Select(th => indent + th.TextContent));
                }
                else if (child.ClassList.Contains("munin-explorer-kilde__delkilder"))
                {
                    foreach (var item in child.Children)
                    {
                        lines.Add($"{indent}[{item.QuerySelector("h3, h4, h5, h6")?.TextContent}]");
                        Walk(item, depth + 1);
                    }
                }
            }
        }

        Walk(cut.Find(".munin-explorer-kilde__main"), 0);

        return lines;
    }

    private static IReadOnlyList<string> BlockHeadings(IRenderedComponent<KildeView> cut) =>
        [.. cut.FindAll(".munin-explorer-kilde__body .headline-s").Select(e => e.TextContent)];

    // ---------------------------------------------------------------------------------
    // Styling contract. The package ships no CSS, so every class name this view emits is
    // a promise that some stylesheet — helsedata's, or the sample one a host copies —
    // already defines it. A name in neither renders as a raw browser default inside an
    // otherwise styled page, which is the failure this package exists to avoid.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_Always_ThenEveryClassNameIsOneSomeStylesheetActuallyDefines()
    {
        // The check that had nowhere to hang. This view is where `headline-sm` lived longest — a
        // typo for `headline-s` on all four block headings, defined nowhere, so every one of them
        // rendered at the browser's own <h*> size on helsedata.no. It reaches the DOM as an argument
        // to @Heading rather than as a class attribute, which puts it out of reach of grep and of
        // the CSS checks in scripts/; rendering the component is the only way to see it.
        //
        // Asked of the study rather than of the plain source: the four names the delkilde tree
        // writes are drawn only when there is a tree to draw, so a source with no delkilder checks
        // every name but those. The study's waves carry a beskrivelse for the same reason — the
        // fourth name is drawn only where one is authored.
        var cut = Render(Study());

        // Compared against an empty list rather than asserted empty, so a failure names the classes
        // instead of saying only that there were some.
        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles()
    {
        // The exact list, for the reason the explorer's own version of this is exact: a tenth name
        // appearing here is news, and news that has to be answered in both sample stylesheets before
        // it ships. None of these was ever helsedata's — the six that used to be theirs in this prefix
        // are all on the explorer, none on this view — so every one is a promise only the sample
        // stylesheet keeps.
        //
        // It is the second such list: VariableSearchTest.cs pins twelve of these fourteen down the
        // drill-in path, all but munin-explorer-group, which that fixture's kilde has no metadata
        // groups to produce, and the delkilde beskrivelse, which its delkilder do not carry.
        // Renaming a handle means editing both, and the other one fails with a message about the
        // explorer rather than about this view.
        //
        // Rendered from the study, so the four delkilde names are inside the list rather than
        // outside it: they are drawn only when the source has a tree, which makes a source without
        // one exactly the render that would let them ship unnamed and unstyled.
        var cut = Render(Study());

        var invented = HostClassNames.Of(cut.FindAll("[class]"))
            .Where(HostClassNames.IsOwnStructureName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
        [
            "munin-explorer-group",                  // shared with the variable view
            "munin-explorer-kilde",
            "munin-explorer-kilde__aside",
            "munin-explorer-kilde__body",
            "munin-explorer-kilde__datasamlinger",
            "munin-explorer-kilde__delkilde",
            "munin-explorer-kilde__delkilde-description",
            "munin-explorer-kilde__delkilde-name",
            "munin-explorer-kilde__delkilder",
            "munin-explorer-kilde__description",
            "munin-explorer-kilde__header",
            "munin-explorer-kilde__identifiers",
            "munin-explorer-kilde__kildetype",
            "munin-explorer-kilde__main",
        ], invented);
    }

    // ---------------------------------------------------------------------------------
    // The name block.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenNoKildeHasArrived_ThenNothingIsDrawnAtAll()
    {
        // The parameter is EditorRequired but the caller sets it from a fetch, so null is the state
        // between opening the view and the payload landing. An empty shell — a header rule, a
        // sidebar box with a heading and no facts — reads as a source with nothing in it.
        Assert.Equal(string.Empty, Render(kilde: null).Markup.Trim());
    }

    [Fact]
    public void Identifiers_WhenTheKildeHasAShortName_ThenItSitsBesideTheCode()
    {
        Assert.Equal("K_ALS (ALS)", Render(Kilde()).Find(".munin-explorer-kilde__identifiers").TextContent);
    }

    [Fact]
    public void Identifiers_WhenTheKildeHasNoShortName_ThenTheCodeStandsAloneWithoutEmptyBrackets()
    {
        var cut = Render(Kilde() with { ShortName = "" });

        Assert.Equal("K_ALS", cut.Find(".munin-explorer-kilde__identifiers").TextContent);
    }

    [Fact]
    public void Identifiers_WhenTheKildeHasNoCode_ThenNoEmptyLineIsDrawnUnderTheName()
    {
        // A line holding only " (ALS)" is worse than no line: it reads as a rendering fault rather
        // than as a catalogue that has not been given a code.
        var cut = Render(Kilde() with { Code = "" });

        Assert.Empty(cut.FindAll(".munin-explorer-kilde__identifiers"));
    }

    [Fact]
    public void Kildetype_WhenTheCatalogueSendsItsEnumName_ThenTheBadgeSaysItInProse()
    {
        Assert.Equal("Nasjonalt medisinsk kvalitetsregister",
                     Render(Kilde()).Find(".munin-explorer-kilde__kildetype").TextContent);
    }

    [Fact]
    public void Kildetype_WhenItIsOneWeHaveNeverSeen_ThenItIsShownRatherThanHidden()
    {
        // Munin's kildetype enum is master data, so a new member is a catalogue change rather than a
        // bug here. "Pasientregister" on a badge is poor prose and true, where dropping it would
        // take the source's category off the screen entirely.
        var cut = Render(Kilde() with { Kildetype = "pasientregister" });

        Assert.Equal("pasientregister", cut.Find(".munin-explorer-kilde__kildetype").TextContent);
    }

    [Fact]
    public void Kildetype_WhenTheKildeHasNone_ThenNoEmptyBadgeIsDrawnButTheSidebarStillSaysSo()
    {
        // A badge is a shape as much as a word, so an empty one is a stray coloured box. The
        // sidebar is a record and answers the question either way — "Ikke oppgitt" is the answer
        // there, and a missing row would leave a reader wondering whether it was asked.
        var cut = Render(Kilde() with { Kildetype = "" });

        Assert.Empty(cut.FindAll(".munin-explorer-kilde__kildetype"));
        Assert.Equal("Ikke oppgitt", Value(SourceInformation(cut), "Type datakilde"));
    }

    [Fact]
    public void Description_WhenTheKildeHasNone_ThenNoEmptyIngressIsDrawn()
    {
        Assert.Empty(Render(Kilde() with { Description = null }).FindAll(".munin-explorer-kilde__description"));
    }

    // ---------------------------------------------------------------------------------
    // Heading level and the id a landmark is named by — the two things that let one view
    // sit both on a page of its own and inside a result the explorer opened.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Headings_WhenTheViewSitsDeeperInThePage_ThenEveryLevelUnderTheNameFollowsIt()
    {
        // Heading order is how a screen reader user navigates, not decoration. A view that always
        // emitted h2 would break the order wherever it opened inside a result row, and one that
        // moved only its own name would leave the blocks under it at a level above their heading.
        var cut = Render(Kilde(), headingLevel: 3);

        Assert.Equal("h3", cut.Find(".munin-explorer-kilde__header .headline-s").TagName, ignoreCase: true);

        // Counted as well as checked: Assert.All passes over an empty collection, so a selector
        // that stopped matching would leave this test green while checking nothing.
        var blocks = cut.FindAll(".munin-explorer-kilde__body .headline-s");
        var groups = cut.FindAll(".munin-explorer-group");

        Assert.Equal(4, blocks.Count);
        Assert.Equal(2, groups.Count);
        Assert.All(blocks, h => Assert.Equal("h4", h.TagName, ignoreCase: true));
        Assert.All(groups, h => Assert.Equal("h5", h.TagName, ignoreCase: true));
    }

    [Fact]
    public void Headings_WhenTheViewSitsAsDeepAsHeadingsGo_ThenTheLevelsStopAtSixRatherThanRunningPastIt()
    {
        // h7 is not a heading, it is an unknown element: a screen reader lists none of them, so the
        // deepest mount would lose the whole structure rather than flatten it.
        var cut = Render(Kilde(), headingLevel: 6);

        var headings = cut.FindAll(".headline-s");
        var groups = cut.FindAll(".munin-explorer-group");

        Assert.Equal(5, headings.Count);
        Assert.Equal(2, groups.Count);
        Assert.All(headings, h => Assert.Equal("h6", h.TagName, ignoreCase: true));
        Assert.All(groups, h => Assert.Equal("h6", h.TagName, ignoreCase: true));
        Assert.Empty(cut.FindAll("h7"));
    }

    [Fact]
    public void HeadingId_WhenTheHostNamesARegionByTheName_ThenTheIdIsOnTheNameAndNowhereElse()
    {
        // The drill-in is a landmark, and a landmark is only useful if a screen reader can say which
        // source it just entered. That means the id has to be on this component's own name — a
        // second heading outside it saying the same thing is a different heading.
        var cut = Render(Kilde(), headingId: "kilde-heading");

        var named = cut.FindAll("#kilde-heading");

        Assert.Single(named);
        Assert.Equal("Als registeret", named[0].TextContent);
    }

    [Fact]
    public void HeadingId_WhenTheHostNamesNothing_ThenNoEmptyIdIsEmitted()
    {
        // An id="" is not nothing: it is an id no aria-labelledby can point at, and two of them on
        // one page are duplicates.
        Assert.Empty(Render(Kilde()).FindAll("[id]"));
    }

    // ---------------------------------------------------------------------------------
    // The metadata the catalogue arranges itself.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Metadata_WhenTheCatalogueGroupsTheProperties_ThenEachGroupIsAHeadingOverItsOwnList()
    {
        // Which groups exist, what they are called and what order they come in all arrive with the
        // payload. The empty one is dropped rather than drawn: a heading promising something over
        // blank rows is the failure this rule was measured against on Runa.
        var cut = Render(Kilde());

        Assert.Equal(["Formål", "Beskrivelse"],
                     cut.FindAll(".munin-explorer-group").Select(e => e.TextContent));
        Assert.DoesNotContain("Datakvalitet", cut.Markup, StringComparison.Ordinal);

        var first = cut.FindAll(".munin-explorer-kilde__main dl")[0];

        Assert.Equal(["Formål med registeret"], Labels(first));
        Assert.Equal(["Kvalitetssikring av behandlingen."], Values(first));
    }

    [Fact]
    public void Metadata_WhenTheCatalogueHasFilledInNothing_ThenNoHeadingPromisesAny()
    {
        var cut = Render(Kilde() with { AdditionalProperties = new Dictionary<string, string?>() });

        // Asked of the block headings rather than of the whole markup, which anything satisfies: a
        // data-metadata handle or a PropertyMetadata key in an attribute would fail a substring
        // check for a reason that has nothing to do with a heading promising a block.
        Assert.DoesNotContain("Metadata", BlockHeadings(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-group"));
    }

    // ---------------------------------------------------------------------------------
    // The same metadata, out of a captured payload rather than a hand-written source.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The live payload, captured: 73 curated keys across thirteen groups, eighteen filled in. The
    /// hand-written sources above carry two groups between them. (Fhi.Metadata-6a8wp)
    /// </summary>
    private static KildeDetail Barnediabetes() =>
        JsonSerializer.Deserialize<KildeDetail>(
            TestData.Read("kilde-barnediabetes.json"), MuninExplorerClient.Json)
        ?? throw new InvalidOperationException("kilde-barnediabetes.json no longer reads as a KildeDetail.");

    [Theory]
    [InlineData("no", new[] { "Datainnsamling", "Beskrivelse", "Formål", "EHDS / HealthDCAT-AP",
                              "Kontakt", "Versjonering", "Helsedatatilgangsorgan (overstyring)" })]
    [InlineData("en", new[] { "Data Collection", "Description", "Purpose", "EHDS / HealthDCAT-AP",
                              "Contact", "Versioning", "Health Data Access Body (override)" })]
    public void Metadata_WhenARealSourceIsDrawn_ThenEveryGroupItFilledInIsThereInTheReadersLanguage(
        string language, string[] expected)
    {
        // Read as a list rather than searched for, so a group that stops being drawn is a failure
        // and not merely unreported, and so the catalogue's own order is asserted with it.
        var cut = Render(Barnediabetes(), language);

        Assert.Equal(expected, cut.FindAll(".munin-explorer-group").Select(e => e.TextContent));
    }

    [Theory]
    [InlineData("no")]
    [InlineData("en")]
    public void Metadata_WhenARealSourceIsDrawn_ThenItsDescriptionIsPrintedOnceAndNotAgainAsAField(
        string language)
    {
        // Counted over the whole render rather than asserted on one element: where the second copy
        // came from is the thing under test, so naming the EHDS group would pass if it moved.
        // (Fhi.Metadata-8yqoz)
        var kilde = Barnediabetes();
        var cut = Render(kilde, language);

        var description = kilde.Description!;

        Assert.True(description.Length > 1000, "the captured description should be the long one");

        // Line by line since Fhi.Metadata-5bcr7: the ingress renders the description's own line
        // breaks as elements, so the raw string no longer appears contiguously anywhere.
        var lines = description.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.True(lines.Length > 1, "the captured description should span several lines");

        foreach (var line in lines)
        {
            Assert.Equal(1, Occurrences(cut.Markup, line));
        }

        // And it is the ingress that kept it, not the field.
        Assert.Contains(lines[0], cut.Find(".munin-explorer-kilde__description").TextContent,
                        StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("no", "EHDS / HealthDCAT-AP")]
    [InlineData("en", "EHDS / HealthDCAT-AP")]
    public void Metadata_WhenTheDescriptionIsExcluded_ThenItsGroupKeepsTheRestOfItsFields(
        string language, string group)
    {
        // THE TRAP: Groups drops a group whose every key is unset, so an exclusion can take the
        // group with it. Five of the six populated EHDS keys survive, so it must still draw rows.
        // The sibling test pins all seven group names; this one catches a hollow heading.
        var kilde = Barnediabetes();
        var cut = Render(kilde, language);

        var heading = cut.FindAll(".munin-explorer-group")
                         .SingleOrDefault(e => e.TextContent == group);

        Assert.NotNull(heading);

        // THAT group's own rows, reached through its sibling <dl>. A global count of <dt> passes
        // while this very group is empty, on the strength of the six other groups' rows — which
        // would leave the test green over exactly the hollow heading it exists to catch.
        var rows = heading!.NextElementSibling;

        Assert.NotNull(rows);
        Assert.Equal("DL", rows!.TagName);
        Assert.True(rows.QuerySelectorAll("dt").Length >= 4,
                    $"the EHDS group should keep its other fields, found {rows.QuerySelectorAll("dt").Length}");

        // And what it lost is the description, not something else.
        Assert.DoesNotContain(kilde.Description!, rows.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Metadata_WhenTheCatalogueHoldsAnEnglishDescription_ThenItIsNotExcludedWithTheNorwegian()
    {
        // THE SECOND TRAP, in the direction that deletes rather than duplicates: the ingress is the
        // Norwegian description whatever the reader's language, so the English text appears nowhere
        // else and excluding it would remove it from the panel rather than de-duplicate it.
        var kilde = Kilde() with
        {
            PropertyMetadata = [Entry("BeskrivelseEngelsk", 10, "Beskrivelse", "Beskrivelse (engelsk)")],
            AdditionalProperties = new Dictionary<string, string?>
            {
                ["BeskrivelseEngelsk"] = "Norwegian register for ALS and other motor neurone diseases.",
            },
        };

        var cut = Render(kilde);

        Assert.Contains("Norwegian register for ALS", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Metadata_WhenOnlyThePlainDescriptionKeyIsCurated_ThenItIsExcludedToo()
    {
        // THE THIRD TRAP. The captured source carries BeskrivelseFlerspraklig, so a fix covering
        // only that key looks complete against it. A source curating the plain Beskrivelse instead
        // would keep the duplicate, and nothing here would have said so.
        var kilde = Kilde() with
        {
            PropertyMetadata = [Entry("Beskrivelse", 10, "Beskrivelse")],
            AdditionalProperties = new Dictionary<string, string?>
            {
                ["Beskrivelse"] = "Norsk register for ALS og andre motonevronsykdommer.",
            },
        };

        var cut = Render(kilde);

        Assert.Equal(1, Occurrences(cut.Markup, kilde.Description!));
    }

    // Throws on an empty needle rather than hanging the run: IndexOf("") answers the position it
    // was asked from and the stride is zero.
    private static int Occurrences(string haystack, string needle)
    {
        ArgumentException.ThrowIfNullOrEmpty(needle);

        var count = 0;

        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal);
             i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [Theory]
    [InlineData("no", new[] { "Identifikasjon", "Kvalitet", "Juridisk", "Identifikatorer", "Samsvar" })]
    [InlineData("en", new[] { "Identification", "Quality", "Legal", "Identifiers", "Compliance" })]
    public void Metadata_WhenARealSourceLeavesAGroupUnset_ThenNoHeadingPromisesIt(
        string language, string[] unset)
    {
        // Five of this source's thirteen groups are curated and empty. A heading with nothing under
        // it counts as a missing section rather than a drawn one.
        var cut = Render(Barnediabetes(), language);

        // Counted first: every assertion below passes over a view that drew no group at all, which
        // is the very regression this source was captured for.
        Assert.NotEmpty(cut.FindAll(".munin-explorer-group"));

        Assert.All(unset, name => Assert.DoesNotContain(
            name, cut.FindAll(".munin-explorer-group").Select(e => e.TextContent)));

        foreach (var heading in cut.FindAll(".munin-explorer-group"))
        {
            Assert.Equal("DL", heading.NextElementSibling?.TagName);
            Assert.NotEmpty(heading.NextElementSibling!.QuerySelectorAll("dd"));
        }
    }

    [Theory]
    [InlineData("no")]
    [InlineData("en")]
    public void Metadata_WhenARealSourceStoresAValuePerLanguage_ThenTheReaderSeesWordsAndNotTheEnvelope(
        string language)
    {
        // Three of this source's values are Flerspraklig siblings, and all three are stored under
        // nb alone: an English host reading only the Norwegian sibling shows them, one reading only
        // an en key that is not there shows blanks, and one reading neither shows the envelope.
        var cut = Render(Barnediabetes(), language);

        var values = cut.FindAll(".munin-explorer-kilde__main dd").Select(e => e.TextContent).ToList();

        Assert.Contains(values, v => v.StartsWith("Barnediabetesregisterets formål er:", StringComparison.Ordinal));
        Assert.Contains("Barnediabetes", values);
        Assert.All(values, v => Assert.DoesNotContain("\"nb\":", v, StringComparison.Ordinal));
        Assert.All(values, v => Assert.DoesNotContain("\"value\":", v, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------
    // The datasamlinger the source holds.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void DataCollections_WhenSomeHangUnderADelkilde_ThenEachSitsInsideTheDelkildeItBelongsTo()
    {
        // The section this view used to draw was one flat table of every datasamling the source
        // holds, gathered through the delkilder and then sorted as if they were one list. It
        // answered what a study series holds and destroyed how it is arranged, which for a study
        // series is usually the question — Tromsø's organising fact is its waves.
        //
        // So: the source's own first, then a delkilde carrying its own, then that delkilde's own
        // delkilde carrying the last one. Both halves of the claim are in this one list. Every
        // datasamling is still reachable, which is what the flat table got right; and each is
        // inside the delkilde it belongs to, which is what it got wrong.
        Assert.Equal(
        [
            "Inklusjon",
            "[Tromsø 4]",
            "  Spørreskjema",
            "  [Første besøk]",
            "    Blodprøver",
        ], Outline(Render(Study())));
    }

    [Fact]
    public void DataCollections_WhenTheKildeHasNoDelkilder_ThenTheTableIsStillTheWholeSection()
    {
        // THE SECOND TRAP. Most kilder have no delkilder at all, so replacing the table with a tree
        // unconditionally would trade a missing structure for missing data on the majority of
        // sources — and every assertion about the tree above would still pass, because none of them
        // renders a source like this one.
        var kilde = Kilde() with
        {
            Datasamlinger = [Collection("Inklusjon"), Collection("Oppfølging")],
            Delkilder = [],
        };

        var cut = Render(kilde);

        Assert.Equal(["Inklusjon", "Oppfølging"], CollectionNames(cut));

        // And no empty list around them: a source with nothing to nest gets exactly the section it
        // has always had, which is the whole of what "looks as it does today" means here.
        Assert.Empty(cut.FindAll("ul.munin-explorer-kilde__delkilder"));
        Assert.Equal(["Inklusjon", "Oppfølging"], Outline(cut));
    }

    [Fact]
    public void Delkilder_WhenOneIsNestedUnderAnother_ThenTheMarkupCarriesItRatherThanTheIndentation()
    {
        // THE THIRD TRAP, and the one an automated accessibility check cannot see: it is blind to
        // structure that was never marked up, so a stack of <div>s indented by CSS passes it while
        // telling a screen-reader user nothing at all. Indentation is not a relationship.
        //
        // A nested <ul>/<li> is, natively and with no keyboard contract to implement — which
        // role="tree" would have obliged, and does not appear here for that reason. So this asks
        // the DOM for the relationship itself rather than for a class name or a computed style.
        var cut = Render(Study());

        var list = cut.Find("ul.munin-explorer-kilde__delkilder");

        Assert.Equal("UL", list.TagName);

        var wave = Assert.Single(list.Children);

        Assert.Equal("LI", wave.TagName);
        Assert.Equal("Tromsø 4", wave.QuerySelector("h4")?.TextContent);

        // The nested wave's list is INSIDE its parent's list item, which is the sentence "Første
        // besøk is part of Tromsø 4" in markup. A second list beside the first would render
        // identically once the sample stylesheet indented it.
        var nested = wave.QuerySelector("ul.munin-explorer-kilde__delkilder");

        Assert.NotNull(nested);
        Assert.Equal("Første besøk", Assert.Single(nested!.Children).QuerySelector("h5")?.TextContent);

        // And the datasamling belongs to the wave rather than to the section: the table it is in
        // sits inside that same list item.
        Assert.Equal("Spørreskjema",
                     wave.QuerySelector(":scope > table.munin-explorer-kilde__datasamlinger tbody th")?.TextContent);
    }

    [Theory]
    [InlineData(2, "H4", "H5")]
    [InlineData(4, "H6", "H6")]
    public void Delkilder_WhenTheViewIsMountedAtALevel_ThenEachDepthIsOneHeadingDeeperThanTheLast(
        int headingLevel,
        string top,
        string nested)
    {
        // Heading order is how a screen reader user navigates a page, so the tree the list draws and
        // the tree the outline draws have to be the same tree: a wave's wave one level deeper than
        // the wave. The second row is the flattening rather than an off-by-one — a title at h4 puts
        // the section at h5 and the first wave at h6, which is where the outline stops, so the
        // nested one stops there too rather than becoming an h7 no browser has.
        var cut = Render(Study(), headingLevel: headingLevel);

        var names = cut.FindAll(".munin-explorer-kilde__delkilde-name");

        Assert.Equal([top, nested], names.Select(e => e.TagName));
        Assert.Equal(["Tromsø 4", "Første besøk"], names.Select(e => e.TextContent));
    }

    [Fact]
    public void Delkilder_WhenOneCarriesACode_ThenItSitsUnderItsName()
    {
        // A delkilde is looked up by its code the way the kilde above it is — K_TR.TR4 — so the line
        // wears the same class name as the kilde's own, being the same thing one level down.
        var cut = Render(Study(), language: "en");

        var wave = cut.Find("ul.munin-explorer-kilde__delkilder > li");

        Assert.Equal("K_TR.TR4", wave.QuerySelector(".munin-explorer-kilde__identifiers")?.TextContent);
    }

    [Fact]
    public void Delkilder_WhenOneCarriesADescription_ThenItIsDrawnUnderTheNameLine()
    {
        // Asserted as an anchor, not as text: the description was held back under
        // Fhi.Metadata-wtz80 because the view could only print it raw, so a text-only check
        // would pass on the very rendering that was refused — brackets and URL on the page.
        var cut = Render(Study(), language: "en");

        var wave = cut.Find("ul.munin-explorer-kilde__delkilder > li");
        var description = wave.QuerySelector("p.munin-explorer-kilde__delkilde-description")!;

        Assert.Equal("Fjerde runde av Tromsøundersøkelsen.", description.TextContent);
        Assert.Equal("https://uit.no/tromsoundersokelsen",
                     description.QuerySelector("a")?.GetAttribute("href"));

        Assert.Equal(
            ["munin-explorer-kilde__delkilde-name",
             "munin-explorer-kilde__identifiers",
             "munin-explorer-kilde__delkilde-description"],
            wave.Children.Take(3).Select(e => e.ClassList.Last()));
    }

    [Fact]
    public void Delkilder_WhenOneCarriesADescription_ThenItIsMarkedAsTheCataloguesLanguage()
    {
        // The catalogue stores one description, in Norwegian, whatever the reader is reading — so an
        // English reader gets it marked and a Norwegian one does not, the rule the kilde's own
        // description and every datasamling cell already follow.
        Assert.Equal("no", Description(Render(Study(), language: "en"))?.GetAttribute("lang"));
        Assert.Null(Description(Render(Study(), language: "no"))?.GetAttribute("lang"));
    }

    [Fact]
    public void Delkilder_WhenOneCarriesNoDescription_ThenNoEmptyParagraphIsDrawnForIt()
    {
        // Most delkilder in the catalogue have none, and an empty <p> is not nothing: it takes the
        // rule's own margin, so every wave without a description would sit further from its table
        // than the ones with.
        var kilde = Kilde() with
        {
            Datasamlinger = [],
            Delkilder = [Delkilde("Biodata", []), Delkilde("Tromsø 4", [], description: "   ")],
        };

        Assert.Empty(Render(kilde).FindAll("p.munin-explorer-kilde__delkilde-description"));
    }

    [Fact]
    public void Delkilder_WhenANestedOneCarriesADescription_ThenItIsDrawnToo()
    {
        // The tree is walked, not just its top: Tromsø's waves nest, and a description drawn only at
        // the first level would leave the deeper ones with the same silence they had before.
        var cut = Render(Study(), language: "en");

        Assert.Equal(
            ["Fjerde runde av Tromsøundersøkelsen.", "Første besøksrunde."],
            cut.FindAll("p.munin-explorer-kilde__delkilde-description").Select(e => e.TextContent));
    }

    /// <summary>The first delkilde's description paragraph, or null where none was drawn.</summary>
    private static IElement? Description(IRenderedComponent<KildeView> cut) =>
        cut.Find("ul.munin-explorer-kilde__delkilder > li")
           .QuerySelector("p.munin-explorer-kilde__delkilde-description");

    [Fact]
    public void Delkilder_WhenTheCatalogueHasOrderedThem_ThenThoseComeFirstAndTheRestAlphabetically()
    {
        // The same two rules the datasamlinger follow, applied at every level of the tree rather
        // than at the top of it: a curated order wins, and the Norwegian alphabet takes the rest.
        // The nested pair is the half a top-level-only sort would get wrong.
        var kilde = Kilde() with
        {
            Datasamlinger = [],
            Delkilder =
            [
                Delkilde("Ålesund", []),
                Delkilde("Bergen", [], order: 2),
                Delkilde("Alta", [],
                         [Delkilde("Åsane", []), Delkilde("Bønes", []), Delkilde("Sandviken", [], order: 1)]),
                Delkilde("Oslo", [], order: 1),
            ],
        };

        Assert.Equal(
        [
            "[Oslo]",
            "[Bergen]",
            "[Alta]",
            "  [Sandviken]",
            "  [Bønes]",
            "  [Åsane]",
            "[Ålesund]",
        ], Outline(Render(kilde, language: "en")));
    }

    [Fact]
    public void Delkilder_WhenOneHoldsNoDatasamlingerOfItsOwn_ThenItIsStillOnThePage()
    {
        // Tromsø really has one: K_TR.BIODATA carries no datasamlinger and is a wave of the study
        // all the same. Drawing only the delkilder that hold something would leave a reader
        // counting six waves on helsedata.no and five here, with nothing saying which is right.
        var kilde = Kilde() with
        {
            Datasamlinger = [],
            Delkilder = [Delkilde("Biodata", []), Delkilde("Tromsø 4", [Collection("Spørreskjema")])],
        };

        var cut = Render(kilde);

        Assert.Equal(["[Biodata]", "[Tromsø 4]", "  Spørreskjema"], Outline(cut));

        // And the section is headed, though not one datasamling hangs off the kilde itself. The
        // heading follows what the source holds anywhere, not what it holds at the top — and it
        // names the delkilder, because there are some.
        Assert.Contains("Delkilder og datasamlinger", BlockHeadings(cut));
    }

    [Fact]
    public void DataCollections_WhenNoCallerNamesTheSection_ThenTheSourceDecidesWhichWordItGets()
    {
        // The heading used to follow the EXPLORER: Runa said "Datasamlinger" and Kelda said
        // "Delkilder og datasamlinger" over identical rows, which was one word of disagreement
        // about one flat table.
        //
        // The section draws the delkilder themselves now, so the Runa wording headed five of
        // Tromsø's waves while promising none of them. Which word is right is a question about the
        // source rather than about who is rendering it, so the source answers it — and a source
        // with no delkilder keeps the old word, because there is nothing else under it to name.
        Assert.Contains("Delkilder og datasamlinger", BlockHeadings(Render(Study())));
        Assert.Contains("Datasamlinger", BlockHeadings(Render(Kilde())));

        // Exact rather than substring, in both directions: "Datasamlinger" IS a substring of
        // "Delkilder og datasamlinger", so a check that only searched the markup would call the
        // wrong heading right. BlockHeadings returns the headings themselves, and Assert.Contains
        // over a list compares whole elements.
        Assert.DoesNotContain("Datasamlinger", BlockHeadings(Render(Study())));
        Assert.DoesNotContain("Delkilder og datasamlinger", BlockHeadings(Render(Kilde())));
    }

    [Fact]
    public void DataCollections_WhenTheCallerNamesTheSection_ThenItsWordWinsOverTheSourcesOwn()
    {
        // Neither explorer passes a heading any more (Fhi.Metadata-rhybi), but a host rendering
        // this view directly still can, and the source-led default must not quietly take that
        // decision back off it. Asked of a source with NO delkilder, where the default and the
        // override disagree — on Study() they agree, and a test that could not tell them apart
        // would pass on an implementation that ignored the parameter entirely.
        Assert.Contains(
            "Delkilder og datasamlinger",
            BlockHeadings(Render(Kilde(), dataCollectionsHeading: "Delkilder og datasamlinger")));
    }

    [Fact]
    public void DataCollections_WhenTheCatalogueHasOrderedSome_ThenThoseComeFirstAndTheRestAlphabetically()
    {
        // Two rules in one list. A curated order is what Munin's own views follow, so it wins; the
        // ones nobody has ordered fall back to the alphabet — the Norwegian one, because the names
        // are the catalogue's and stored once in Norwegian, so å sorts last whoever is reading.
        //
        // Two pairs, because "Norwegian" is two claims and Ålesund only carries one of them. Å above
        // Alta is what an English reader's collation would get wrong, since English folds Å to A —
        // but it is also what a plain byte comparison gets right by accident, U+00C5 being above
        // every ASCII letter, so a comparer with no collation at all would pass on that pair alone.
        // Élan before Fana is the other half: Norwegian sorts É with E, ordinal puts U+00C9 after F.
        var kilde = Kilde() with
        {
            Datasamlinger =
            [
                Collection("Ålesund"),
                Collection("Bergen", order: 2),
                Collection("Fana"),
                Collection("Alta"),
                Collection("Élan"),
                Collection("Oslo", order: 1),
            ],
        };

        Assert.Equal(["Oslo", "Bergen", "Alta", "Élan", "Fana", "Ålesund"],
                     CollectionNames(Render(kilde, language: "en")));
    }

    [Fact]
    public void DataCollections_Always_ThenEachRowIsHeadedByItsNameAndSaysWhatRunaShows()
    {
        var kilde = Kilde() with
        {
            Datasamlinger =
            [
                Collection("Inklusjon", shortName: "INK", description: "Alle pasienter ved inklusjon.",
                           from: new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero), variables: 12),
            ],
        };

        var cut = Render(kilde);

        Assert.Equal(["Navn", "Beskrivelse", "Gyldighet", "Totalt antall variabler"],
                     cut.FindAll("table.munin-explorer-kilde__datasamlinger thead th").Select(e => e.TextContent));

        // A th rather than a td, and scoped to its row: a screen reader reading a cell out of
        // context has to be able to hear which datasamling the number belongs to.
        var name = cut.Find("table.munin-explorer-kilde__datasamlinger tbody th");

        Assert.Equal("row", name.GetAttribute("scope"));
        Assert.Equal("Inklusjon (INK)", name.TextContent);

        Assert.Equal(["Alle pasienter ved inklusjon.", "1. januar 2010 – Pågående", "12 variabler"],
                     cut.FindAll("table.munin-explorer-kilde__datasamlinger tbody td").Select(e => e.TextContent));
    }

    [Fact]
    public void DataCollections_WhenARowStoppedCollecting_ThenTheRowSaysWhenRatherThanOngoing()
    {
        // The same open/closed pair as the sidebar's, asked of the table, because the row's period
        // is the cell a reader uses to tell a wave that has closed from one still running.
        var kilde = Kilde() with
        {
            Datasamlinger =
            [
                Collection("Inklusjon", description: "Alle pasienter ved inklusjon.",
                           from: new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
                           to: new DateTimeOffset(2019, 9, 30, 0, 0, 0, TimeSpan.Zero), variables: 12),
            ],
        };

        // The whole row rather than one cell of it, so the assertion says which column it is reading.
        Assert.Equal(["Alle pasienter ved inklusjon.", "1. januar 2010 – 30. september 2019", "12 variabler"],
                     Render(kilde).FindAll("table.munin-explorer-kilde__datasamlinger tbody td")
                                  .Select(e => e.TextContent));
    }

    [Fact]
    public void DataCollections_WhenTheKildeHasNone_ThenNoHeadingPromisesAny()
    {
        var cut = Render(Kilde() with { Datasamlinger = [], Delkilder = [] });

        Assert.Empty(cut.FindAll("table.munin-explorer-kilde__datasamlinger"));
        Assert.Empty(cut.FindAll("ul.munin-explorer-kilde__delkilder"));
        Assert.DoesNotContain("Datasamlinger", BlockHeadings(cut));
    }

    [Fact]
    public void DataCollections_WhenTheExplorerCallsThemSomethingElse_ThenItsOwnHeadingIsUsed()
    {
        // A caller's own word wins over the source's, and the default has to survive the caller
        // supplying nothing — which is what both explorers do.
        Assert.Contains("Delkilder og datasamlinger",
                        BlockHeadings(Render(Kilde(), dataCollectionsHeading: "Delkilder og datasamlinger")));

        Assert.Contains("Datasamlinger", BlockHeadings(Render(Kilde())));
    }

    // ---------------------------------------------------------------------------------
    // The slot each explorer puts its own sections in.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Sections_WhenAnExplorerPassesThem_ThenTheyComeLastInTheMainColumnRatherThanInTheSidebar()
    {
        // The whole reason this is a core with a slot instead of one view with a flag per Kelda
        // section. They go after the metadata — the source's own record reads first — and in the
        // main column, because the sidebar is the facts every source has and nothing else.
        var cut = Render(Kilde(), sections: KeldaSections);

        var main = cut.Find(".munin-explorer-kilde__main");

        Assert.Equal("kelda-sections", main.Children.Last().Id);
        Assert.Empty(cut.FindAll(".munin-explorer-kilde__aside #kelda-sections"));
    }

    [Fact]
    public void Sections_WhenNoExplorerPassesAny_ThenNothingIsDrawnWhereTheyWouldHaveGone()
    {
        // The datasamling table is the last thing in the column when the slot is empty — no empty
        // wrapper, which would be a stray margin under every source Runa shows.
        var cut = Render(Kilde());

        Assert.Equal("table", cut.Find(".munin-explorer-kilde__main").Children.Last().TagName,
                     ignoreCase: true);
    }

    // ---------------------------------------------------------------------------------
    // The sidebar: the facts every source has, which is why they are typed fields rather
    // than curated properties.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void SourceInformation_Always_ThenItIsTheFieldsRunaShowsInRunasOrder()
    {
        var cut = Render(Kilde());

        Assert.Equal(
            ["Type datakilde", "Lovverk", "Dataansvarlig", "Databehandler",
             "Grad av personidentifikasjon", "Gyldighet", "Sist oppdatert i Munin"],
            Labels(SourceInformation(cut)));

        var facts = SourceInformation(cut);

        Assert.Equal("Nasjonalt medisinsk kvalitetsregister", Value(facts, "Type datakilde"));

        // Separately, and from two different fields in the fixture: the dataansvarlig and the
        // databehandler are adjacent tuples reading adjacent properties, which is where a
        // copy-paste slip attributes a register's data to the wrong organisation.
        Assert.Equal("St. Olavs hospital HF", Value(facts, "Dataansvarlig"));
        Assert.Equal("Hemit HF", Value(facts, "Databehandler"));

        Assert.Equal("Indirekte identifiserbar", Value(facts, "Grad av personidentifikasjon"));
        Assert.Equal("4. mars 2026", Value(facts, "Sist oppdatert i Munin"));
    }

    [Fact]
    public void SourceInformation_WhenTheCatalogueHasNotFilledInAField_ThenNoBlankRowIsDrawnForIt()
    {
        // A dt with an empty dd reads as a value that failed to draw. The two that stay are the two
        // this package writes itself — a kildetype and an identification level always resolve to a
        // word, "Ikke oppgitt" included.
        var kilde = Kilde() with
        {
            LegalBasis = null,
            DataController = "",
            DataProcessor = "   ",
            ValidFrom = null,
            ValidTo = null,
        };

        Assert.Equal(["Type datakilde", "Grad av personidentifikasjon", "Sist oppdatert i Munin"],
                     Labels(SourceInformation(Render(kilde))));
    }

    [Fact]
    public void Statistics_Always_ThenItIsTheVariableCountAndThePeriodTheDataCovers()
    {
        var cut = Render(Kilde());

        Assert.Equal(["Totalt antall variabler", "Dataperiode"], Labels(Statistics(cut)));
        Assert.Equal("312", Value(Statistics(cut), "Totalt antall variabler"));
    }

    [Fact]
    public void Statistics_WhenTheCatalogueKnowsNoDataDates_ThenNoEmptyPeriodRowIsDrawn()
    {
        var cut = Render(Kilde() with { DataFrom = null, DataTo = null });

        Assert.Equal(["Totalt antall variabler"], Labels(Statistics(cut)));
    }

    [Fact]
    public void Period_WhenTheEndIsOpen_ThenItSaysSoRatherThanSittingBlankOrGuessingADate()
    {
        // An open end is the normal case for a register that is still collecting. A blank half reads
        // as a missing value, and a guessed date would be a claim the catalogue never made.
        Assert.Equal("1. januar 2023 – Pågående",
                     Value(SourceInformation(Render(Kilde())), "Gyldighet"));
    }

    [Fact]
    public void Period_WhenTheEndIsClosed_ThenBothDatesAreShownTheWayTheReadersLanguageWritesThem()
    {
        // A register that stopped collecting is a normal catalogue state, and it was the untested
        // half of this view's own Period: every other fixture here leaves the end open, so the end
        // date, the en-dash that joins the two and the English form of the closing date never ran.
        var closed = Kilde() with
        {
            ValidTo = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            DataTo = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
        };

        var norwegian = Render(closed);

        Assert.Equal("1. januar 2023 – 31. desember 2024", Value(SourceInformation(norwegian), "Gyldighet"));
        Assert.Equal("1. januar 2010 – 30. juni 2024", Value(Statistics(norwegian), "Dataperiode"));

        var english = Render(closed, language: "en");

        Assert.Equal("1 January 2023 – 31 December 2024", Value(SourceInformation(english), "Validity"));
        Assert.Equal("1 January 2010 – 30 June 2024", Value(Statistics(english), "Data period"));
    }

    [Fact]
    public void Period_WhenOnlyTheEndIsKnown_ThenTheDateStandsAloneRatherThanBesideABlankHalf()
    {
        // The third shape, and the one with no good answer: an en-dash with nothing before it reads
        // as a value that failed to draw, and a start date the catalogue never gave would be an
        // invention. So the end stands alone — which does read as a start, and is pinned here
        // because it is a decision rather than an accident. VariableView's own copy of Period does
        // the same, so changing it is a change to both.
        var kilde = Kilde() with
        {
            ValidFrom = null,
            ValidTo = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
        };

        Assert.Equal("31. desember 2024", Value(SourceInformation(Render(kilde)), "Gyldighet"));
    }

    // ---------------------------------------------------------------------------------
    // Language. The catalogue stores one name, one description and one set of free-text
    // values, all in Norwegian, whatever language the reader is reading in.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Language_WhenTheReaderIsEnglish_ThenOurOwnWordsFollowThem()
    {
        var cut = Render(Kilde(), language: "en");

        Assert.Equal(["Metadata", "Data collections", "Source information", "Statistics"],
                     BlockHeadings(cut));

        // The kildetype and the identification level are vocabularies this package translates, so
        // they follow the reader too — unlike everything the catalogue wrote, which does not.
        Assert.Equal("National medical quality registry",
                     cut.Find(".munin-explorer-kilde__kildetype").TextContent);
        Assert.Equal("Indirectly identifiable",
                     Value(SourceInformation(cut), "Level of personal identification"));
        Assert.Equal(["Total number of variables", "Data period"], Labels(Statistics(cut)));
    }

    [Fact]
    public void Language_WhenTheReaderIsEnglish_ThenTheCataloguesOwnWordsAreMarkedNorwegianAndOursAreNot()
    {
        // The lang attribute's only job is to pick the voice. Marking our own English prose as
        // Norwegian tells a screen reader to pronounce English by Norwegian rules, which is worse
        // than leaving it unmarked — and leaving the catalogue's Norwegian unmarked on an English
        // page is the same fault the other way round.
        var kilde = Kilde() with
        {
            Datasamlinger = [Collection("Inklusjon", description: "Alle pasienter ved inklusjon.")],
        };

        var cut = Render(kilde, language: "en");

        Assert.Equal("no", cut.Find(".munin-explorer-kilde__header .headline-s").GetAttribute("lang"));
        Assert.Equal("no", cut.Find(".munin-explorer-kilde__description").GetAttribute("lang"));
        Assert.Equal("no", cut.Find("table.munin-explorer-kilde__datasamlinger tbody th").GetAttribute("lang"));

        // Ours: the kildetype badge is this package's translation of an enum, not the catalogue's
        // prose, and so is the identification level beside it in the sidebar.
        Assert.False(cut.Find(".munin-explorer-kilde__kildetype").HasAttribute("lang"));

        var facts = SourceInformation(cut);

        Assert.False(Fact(facts, "Type of data source").HasAttribute("lang"));   // ours
        Assert.Equal("no", Fact(facts, "Legal basis").GetAttribute("lang"));     // the catalogue's
    }

    [Fact]
    public void Language_WhenTheReaderIsNorwegian_ThenNothingIsMarkedAtAll()
    {
        // Text already in the reader's language is left unmarked so it inherits from the host. A
        // lang="no" on every element would be noise, and noise a host cannot override.
        var cut = Render(Kilde());

        Assert.Empty(cut.FindAll("[lang]"));
    }

    [Fact]
    public void Dates_WhenTheReaderIsEnglish_ThenTheyAreWrittenTheWayEnglishWritesThem()
    {
        // The dot after the day is not punctuation, it is what makes the number an ordinal in
        // Norwegian. English does not use it, so an English page carrying the Norwegian skeleton
        // with English month names — "1. January 2023" — is neither language.
        Assert.Equal("1 January 2023 – Ongoing",
                     Value(SourceInformation(Render(Kilde(), language: "en")), "Validity"));
    }

    [Fact]
    public void Render_WhenTheCataloguePropertiesArriveAsNull_ThenTheViewStillDraws()
    {
        // Deserialised rather than constructed, because the constructed shape is the easy half and
        // the payload is the claim: AdditionalProperties is declared non-nullable with an
        // initialiser, and that initialiser only survives a key ABSENT from the payload.
        // System.Text.Json writes null straight over it for an explicit "additionalProperties":
        // null, so the guarantee the type appears to give is not one it has.
        //
        // Both halves are needed to reach the fault. An empty propertyMetadata never looks a key up,
        // so the entry below is what turns the null into a dereference — one that happens while
        // rendering, past the try/catch around the fetch, taking the whole detail view down where a
        // failed load would have been reported. KildeExplorer.Property guards the list against the
        // same shape one click earlier; this is the other half of that answer.
        var kilde = JsonSerializer.Deserialize<KildeDetail>(
            """
            {
              "id": "6f1d4a5c-0000-4000-8000-000000000001",
              "code": "K_ALS",
              "preferredTerm": "Als registeret",
              "kildetype": "nasjonaltMedisinskKvalitetsregister",
              "additionalProperties": null,
              "propertyMetadata": [
                {
                  "key": "Datakilde",
                  "sortOrder": 1,
                  "groupTranslations": { "no": "Om kilden" },
                  "displayNameTranslations": { "no": "Datakilde" }
                }
              ]
            }
            """)!;

        Assert.Null(kilde.AdditionalProperties);

        var cut = Render(kilde);

        // The name survives, which is the point: a source with no curated properties is a source
        // with no curated properties, not a source that cannot be shown.
        Assert.Equal("Als registeret", cut.Find(".munin-explorer-kilde__header .headline-s").TextContent.Trim());

        // And nothing is invented in their place — no heading promising a group that has no rows.
        Assert.Empty(cut.FindAll(".munin-explorer-group"));
    }

    [Fact]
    public void Metadata_WhenAFieldHoldsTwoLanguages_ThenBothAreDrawnAndEachSaysWhichItIs()
    {
        // Rendered rather than resolved, because the resolution is only half the bead: a reader
        // who cannot see which language they are looking at has gained a second paragraph and
        // nothing else (Fhi.Metadata-l9d5r).
        var kilde = Kilde() with
        {
            PropertyMetadata =
            [
                new PropertyMetadataEntry
                {
                    Key = "TittelFlerspraklig",
                    SortOrder = 540,
                    GroupTranslations = new Dictionary<string, string> { ["no"] = "EHDS / HealthDCAT-AP" },
                    DisplayNameTranslations = new Dictionary<string, string> { ["no"] = "Tittel" },
                    Type = "MultilingualText",
                },
            ],
            AdditionalProperties = new Dictionary<string, string?>
            {
                ["TittelFlerspraklig"] = """{"nb":"Als registeret","en":"The ALS registry"}""",
            },
        };

        var markers = Render(kilde).FindAll("p.munin-explorer-meta__language");

        Assert.Equal(["Norsk", "Engelsk"], markers.Select(m => m.TextContent.Trim()));

        var cells = markers.Select(m => m.ParentElement!).ToList();

        // One term with two descriptions, which is what a dl says with two dd under one dt — and
        // not two rows, which would read as two different fields.
        Assert.All(cells, c => Assert.Equal("DD", c.TagName));

        // The pair is wrapped in a div inside the dl. Pinned because a stylesheet cannot see it:
        // a `dl > dd` rule reaches nothing here, which is how the sample's spacing rule for these
        // cells was written unmatchable the first time (PR 149 review).
        var wrapper = Assert.Single(cells.Select(c => c.ParentElement!).Distinct());

        Assert.Equal("DIV", wrapper.TagName);
        Assert.Equal("DL", wrapper.ParentElement!.TagName);
        Assert.Contains("munin-explorer-meta__grid", wrapper.ParentElement.ClassName!, StringComparison.Ordinal);

        var values = cells.Select(c => c.QuerySelector("span")!).ToList();

        Assert.Equal(["Als registeret", "The ALS registry"], values.Select(v => v.TextContent.Trim()));

        // The Norwegian carries no lang at all: it is the reader's, so Foreign drops it and it
        // inherits the host's. Marking it would be the defect this bead came from, inverted.
        Assert.Null(values[0].GetAttribute("lang"));
        Assert.Equal("en", values[1].GetAttribute("lang"));

        // And the name of the language is outside the marked span, so a screen reader does not
        // announce the Norwegian word "Engelsk" in an English voice.
        Assert.Null(markers[1].GetAttribute("lang"));
    }

    [Fact]
    public void Description_WhenTheCatalogueAuthoredMarkdown_ThenTheIngressRendersItAsElements()
    {
        // The Tromsø study's beskrivelse carries <br> tags and markdown links, which the ingress
        // used to print as source text (FHIDev/Munin#5385).
        var kilde = Kilde() with
        {
            Description = "Befolkningsundersøkelse.<br>Se [UiT](https://uit.no/research/tromsostudy).",
        };

        var ingress = Render(kilde).Find(".munin-explorer-kilde__description");

        Assert.Single(ingress.QuerySelectorAll("br"));
        var anchor = Assert.Single(ingress.QuerySelectorAll("a"));
        Assert.Equal("https://uit.no/research/tromsostudy", anchor.GetAttribute("href"));
        Assert.Equal("noopener noreferrer", anchor.GetAttribute("rel"));
        Assert.DoesNotContain("&lt;br&gt;", ingress.InnerHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DataCollections_WhenADescriptionIsAMarkdownLink_ThenTheCellRendersAnAnchor()
    {
        // The datasamling rows are where the catalogue authors bare markdown links most often:
        // '[Tromsø1 - The First Tromsø Study](https://uit.no/...)' printed whole (FHIDev/Munin#5385).
        var kilde = Kilde() with
        {
            Datasamlinger =
            [
                Collection("Tromsø 1",
                           description: "[Tromsø1 - The First Tromsø Study](https://uit.no/research/tromsostudy/project?pid=708230)"),
            ],
        };

        var cell = Render(kilde).Find(".munin-explorer-kilde__datasamlinger tbody td");

        var anchor = Assert.Single(cell.QuerySelectorAll("a"));
        Assert.Equal("https://uit.no/research/tromsostudy/project?pid=708230", anchor.GetAttribute("href"));
        Assert.Equal("Tromsø1 - The First Tromsø Study", anchor.TextContent);
    }

    [Fact]
    public void Properties_WhenAFieldIsTypedUrl_ThenItsRowLinksInsteadOfPrintingTheMarkdown()
    {
        // Hjemmeside is stored as [https://uit.no/...](https://uit.no/...) and declared a Url — a
        // field that exists to be followed, shown for two years as its own source (FHIDev/Munin#5385).
        var kilde = Kilde() with
        {
            PropertyMetadata =
            [
                Entry("Hjemmeside", 10, "Kontakt") with { Type = "Url" },
            ],
            AdditionalProperties = new Dictionary<string, string?>
            {
                ["Hjemmeside"] = "[https://uit.no/research/tromsostudy](https://uit.no/research/tromsostudy)",
            },
        };

        var anchor = Render(kilde).Find(".munin-explorer-meta__grid dd a");

        Assert.Equal("https://uit.no/research/tromsostudy", anchor.GetAttribute("href"));
        Assert.Equal("noopener noreferrer", anchor.GetAttribute("rel"));
        Assert.Equal("https://uit.no/research/tromsostudy", anchor.TextContent);

        // A URL is prose in no language, so the cell must not claim Norwegian (WCAG 3.1.2).
        Assert.Null(anchor.ParentElement!.GetAttribute("lang"));
    }

    [Fact]
    public void Properties_WhenAUrlIsStoredSchemeless_ThenHttpsIsAssumedAndTheCellStaysUnmarked()
    {
        // The other captured Hjemmeside shape: www.barnediabetes.no, no scheme, which an href
        // would resolve as a relative path rather than an address.
        var kilde = Kilde() with
        {
            PropertyMetadata = [Entry("Hjemmeside", 10, "Kontakt") with { Type = "Url" }],
            AdditionalProperties = new Dictionary<string, string?> { ["Hjemmeside"] = "www.barnediabetes.no" },
        };

        var anchor = Render(kilde).Find(".munin-explorer-meta__grid dd a");

        Assert.Equal("https://www.barnediabetes.no", anchor.GetAttribute("href"));
        Assert.Equal("www.barnediabetes.no", anchor.TextContent);
        Assert.Null(anchor.ParentElement!.GetAttribute("lang"));
    }
}
