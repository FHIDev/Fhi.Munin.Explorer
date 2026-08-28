using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The one thing the two explorers must not agree on: the sections Kelda draws over a kilde and
/// Runa does not.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="KildeView"/> is one component both explorers render a source with — measured on
/// 2026-08-20, the same kilde drew the same name block, the same eight metadata groups in the same
/// order and the same two sidebar boxes in both. Kelda then adds Variabler, Kriterier for tilgang
/// til data and Priser, and says "Delkilder og datasamlinger" where Runa says "Datasamlinger" over
/// the same rows. That difference is markup Kelda passes <em>into</em> the shared core, plus one
/// heading it passes as a parameter — never markup added to the core.
/// </para>
/// <para>
/// The trap is not in the first test, and it takes three more to close. An implementation that
/// moved Kelda's three sections inside <see cref="KildeView"/> would pass every assertion about
/// Kelda's own page — same sections, same order, same level — so the second test renders the same
/// kilde out of the same fixture in <em>Runa</em>, where those sections must not be. That catches
/// the move only if it was unconditional: <c>DataCollectionsHeading</c> is the one parameter Kelda
/// passes and Runa does not, so <c>@if (DataCollectionsHeading is not null)</c> around the same
/// markup inside the core passes both. The third test is what that one cannot survive — the core
/// rendered directly, with Kelda's parameters and none of Kelda's markup, asserting its own four
/// blocks are the whole of what it draws. The fourth catches the same change from the side no
/// render can look at, the core's own text, and it greps the identifiers a section is written with
/// rather than the Norwegian words it displays: identifiers here are English and the words live in
/// <see cref="Texts"/>, so the Norwegian never reaches that file even when the leak does.
/// </para>
/// </remarks>
public class KildeSectionsTest : BunitContext
{
    /// <summary>
    /// The Tromsø study, out of the captured payload — a kilde with a real delkilde tree.
    /// </summary>
    /// <remarks>
    /// The fixture rather than a hand-written source, because the datasamling section is the one
    /// both explorers draw and its rows come through five delkilder here. A source with a flat list
    /// of datasamlinger would leave the two views agreeing for a reason that is not the one under
    /// test.
    /// </remarks>
    private static KildeDetail Tromso() =>
        JsonSerializer.Deserialize<KildeDetail>(
            TestData.Read("kilde-med-delkilder.json"), MuninExplorerClient.Json)
        ?? throw new InvalidOperationException("kilde-med-delkilder.json no longer reads as a KildeDetail.");

    /// <summary>Answers Kelda's one list call with the fixture's kilde, and its detail call with the fixture.</summary>
    private sealed class KeldaClient(KildeDetail kilde) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default)
        {
            var summary = new KildeSummary
            {
                Id = kilde.Id,
                Code = kilde.Code,
                Name = kilde.PreferredTerm,
                Kildetype = kilde.Kildetype,
                IsActive = true,
                DelkildeCount = kilde.Delkilder.Count,
                DatasamlingCount = kilde.Datasamlinger.Count,
                TotalVariables = kilde.TotalVariables,
            };

            return Task.FromResult<IReadOnlyList<KildeSummary>>([summary]);
        }

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(id == kilde.Id ? kilde : null);
    }

    /// <summary>
    /// Answers Runa's search with one variable belonging to the same kilde, so the drill-in the
    /// reader opens from a result row lands on the fixture.
    /// </summary>
    private sealed class RunaClient(KildeDetail kilde) : EmptyMuninExplorerClient
    {
        private static readonly Guid VariableId = new("aaaaaaaa-0000-0000-0000-000000000001");

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            var row = new VariableSummary
            {
                Id = VariableId,
                Code = "V_TR.SPM1",
                PreferredTerm = "1. Spørsmål",
                KildeId = kilde.Id,
                KildeName = kilde.PreferredTerm,
            };

            return Task.FromResult(new Page<VariableSummary>
            {
                Items = [row],
                TotalCount = 1,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });
        }

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(new VariableDetail
            {
                Id = id,
                Code = "V_TR.SPM1",
                PreferredTerm = "1. Spørsmål",
                KildeId = kilde.Id,
                KildeName = kilde.PreferredTerm,
                KildeType = kilde.Kildetype,
            });

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(id == kilde.Id ? kilde : null);
    }

    /// <summary>Open the fixture's kilde in Kelda, the way a reader does: from a row in the list.</summary>
    /// <remarks>
    /// <paramref name="language"/> is left unset rather than defaulted to "no" so the common case
    /// renders the component the way a host that names no language does.
    /// </remarks>
    private IRenderedComponent<KildeExplorer> OpenInKelda(
        KildeDetail kilde, int? headingLevel = null, string? language = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(new KeldaClient(kilde));

        var cut = Render<KildeExplorer>(b =>
        {
            if (headingLevel is { } level)
            {
                b.Add(c => c.HeadingLevel, level);
            }

            if (language is not null)
            {
                b.Add(c => c.Language, language);
            }
        });

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        return cut;
    }

    /// <summary>
    /// Open the same kilde in Runa, the way a reader does there: a result row, then its kilde.
    /// </summary>
    /// <remarks>
    /// Through the explorer rather than by rendering <see cref="KildeView"/> with Runa's parameters
    /// by hand. The claim being tested is about what Runa's page shows, and a hand-built render is
    /// a restatement of what this test believes Runa passes — which is the thing that would be
    /// wrong if somebody wired Kelda's sections into the shared view.
    /// </remarks>
    private IRenderedComponent<VariableExplorer> OpenInRuna(KildeDetail kilde)
    {
        Services.AddSingleton<IMuninExplorerClient>(new RunaClient(kilde));

        var cut = Render<VariableExplorer>();

        cut.FindAll("ul.munin-explorer-data-list .munin-explorer-dataitem-main__name")[0].Click();
        cut.FindAll(".munin-explorer-detail > button[id]")[0].Click();

        return cut;
    }

    /// <summary>
    /// The headings over the blocks of an open kilde — the same selector in both explorers, because
    /// the markup under it is the same component.
    /// </summary>
    /// <remarks>
    /// Scoped to the view's body, which is what leaves the source's own name out: it wears
    /// <c>headline-s</c> too, in the header block above. Read as a list rather than searched for, so
    /// a section that appears where it should not is a failure and not merely unreported.
    /// </remarks>
    private const string BlockHeadings = ".munin-explorer-kilde__body .headline-s";

    /// <summary>The datasamling rows, each headed with its own name.</summary>
    private const string CollectionRows = "table.munin-explorer-kilde__datasamlinger tbody th";

    private static IReadOnlyList<string> TextOf(IEnumerable<IElement> elements) =>
        [.. elements.Select(e => e.TextContent.Trim())];

    [Fact]
    public void Kelda_WhenAKildeIsOpen_ThenItsFourSectionsAreThereWithTheSharedCoreUnderneath()
    {
        var kilde = Tromso();

        var cut = OpenInKelda(kilde);

        // The whole page, in order: the shared core's metadata, the datasamling section under
        // Kelda's word for it, Kelda's own three, and the core's two sidebar boxes. Nothing here is
        // a second copy of the metadata block or the sidebar — those are the core's, once.
        Assert.Equal(
        [
            "Metadata",
            "Delkilder og datasamlinger",
            "Variabler",
            "Kriterier for tilgang til data",
            "Priser",
            "Kildeinformasjon",
            "Statistikk",
        ], TextOf(cut.FindAll(BlockHeadings)));

        // The fixture really rendered, and through the delkilder rather than only off the kilde:
        // three datasamlinger hang directly off the Tromsø study and eleven under its five
        // delkilder. A section headed "Delkilder og datasamlinger" over the three would be the
        // failure this fixture exists to catch.
        var rows = TextOf(cut.FindAll(CollectionRows));

        Assert.Equal(14, rows.Count);
        Assert.Contains("Tromsø1 - The First Tromsø Study", rows);
        Assert.Contains("Tromsø4 - The Fourth Tromsø Study - first visit", rows);

        // Kelda's own sections have bodies rather than being bare headings: a heading with nothing
        // under it reads as a rendering fault to a reader who cannot know a section is unfinished.
        //
        // Read pairwise out of the paragraph under each heading, for the reason the English test
        // states and this language needs more: Norwegian is what a host that names no language
        // gets, so it is the page most readers see. Two Contains over the markup are satisfied
        // just as happily by two bodies that have swapped places — transpose the two sentences at
        // the Norwegian construction site and the page shows the prices sentence under "Kriterier
        // for tilgang til data" with the whole suite still green. Named arguments do not catch it
        // either: the wrong string against the right name compiles and reads correctly.
        //
        // One substring per body, each unique to it, rather than the tail the two static sentences
        // happen to share — both end "…er beskrevet på helsedata.no.".
        Assert.Equal(
            "5752 publiserte variabler i denne kilden.",
            BodyUnder(cut, "Variabler"));
        Assert.StartsWith(
            "Kriteriene for tilgang til data fra denne kilden",
            BodyUnder(cut, "Kriterier for tilgang til data"),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Prisene for utlevering av data fra denne kilden",
            BodyUnder(cut, "Priser"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DataCollections_WhenTheStudySeriesIsOpen_ThenEachWavesOwnSitInsideTheWave()
    {
        // THE TRAP, and the reason this test is here and not only in KildeViewTest: do not verify
        // the arrangement on a kilde without delkilder. Most kilder have none, and on those the
        // arranged section and the flat table it replaced draw the same picture, so a pass there
        // proves nothing ran. This is the captured Tromsø payload — a study series whose organising
        // fact is its waves, three datasamlinger on the study itself and eleven under its five
        // delkilder — rather than a source written to suit the assertion.
        //
        // Read by descending through the <li> each row is in. A flat table holds all fourteen names
        // too and would satisfy any assertion that only counted them, which is exactly what the
        // section did before: it answered what the study holds while destroying how it is arranged.
        var cut = OpenInKelda(Tromso());

        // The study's own three, in the table outside the list — the datasamlinger that hang off
        // the kilde rather than off a wave.
        Assert.Equal(
        [
            "Tromsø1 - The First Tromsø Study",
            "Tromsø2 - The Second Tromsø Study",
            "Tromsø3 - The Third Tromsø Study",
        ], TextOf(cut.FindAll(
            ".munin-explorer-kilde__main > table.munin-explorer-kilde__datasamlinger tbody th")));

        // Then the five waves, in the catalogue's order, each with what is inside it. K_TR.BIODATA
        // holds nothing and is a wave of the study all the same: drawing only the delkilder that
        // hold something would leave a reader counting five waves on helsedata.no and four here.
        Assert.Equal(
        [
            "Biodata:",
            "Tromsø4 - The Fourth Tromsø Study: "
            + "Tromsø4 - The Fourth Tromsø Study - first visit, "
            + "Tromsø4 - The Fourth Tromsø Study - second visit",
            "Tromsø5 - The Fifth Tromsø Study: "
            + "Tromsø5 - The Fifth Tromsø Study - first visit, "
            + "Tromsø5 - The Fifth Tromsø Study - second visit, "
            + "Tromsø5 - The Fifth Tromsø study - forst visit ; sample collection, "
            + "Tromsø5 - The Fifth Tromsø study - second visit; sample collection",
            "Tromsø6 - The Sixth Tromsø Study: "
            + "Tromsø6 - The Sixth Tromsø Study - first visit, "
            + "Tromsø6 - The Sixth Tromsø Study - second visit",
            "Tromsø7 - The Seventh Tromsø Study: "
            + "Tromsø7 - The Seventh Tromsø Study - first visit, "
            + "Tromsø7 - The Seventh Tromsø Study - second visit, "
            + "Tromsø7 - The Seventh Tromsø Study -Sample collection",
        ], Waves(cut));
    }

    /// <summary>
    /// Every delkilde on the page, as its name and the datasamlinger inside its own list item.
    /// </summary>
    /// <remarks>
    /// <c>:scope &gt;</c> on the table is the whole assertion. Without it a wave would report the
    /// datasamlinger of every wave nested below it as its own, which is the flattening this section
    /// exists to undo — and the test would pass on the implementation that has it.
    /// </remarks>
    private static IReadOnlyList<string> Waves(IRenderedComponent<KildeExplorer> cut) =>
        [.. cut.FindAll("li.munin-explorer-kilde__delkilde")
               .Select(item =>
               {
                   var name = item.QuerySelector(".munin-explorer-kilde__delkilde-name")?.TextContent.Trim();

                   var rows = item
                       .QuerySelectorAll(":scope > table.munin-explorer-kilde__datasamlinger tbody th")
                       .Select(th => th.TextContent.Trim());

                   return $"{name}: {string.Join(", ", rows)}".TrimEnd();
               })];

    [Fact]
    public void Runa_WhenTheSameKildeIsOpen_ThenNoneOfKeldasSectionsAreOnIt()
    {
        // THE TRAP. Kelda's sections put inside the shared view behind a condition would pass every
        // assertion above and fail only here — which is why this renders the same source, out of
        // the same fixture, in the other explorer.
        var kilde = Tromso();

        var cut = OpenInRuna(kilde);

        Assert.Equal(
            ["Metadata", "Datasamlinger", "Kildeinformasjon", "Statistikk"],
            TextOf(cut.FindAll(BlockHeadings)));

        // Said again over the text rather than only over the headings: a section reduced to a
        // paragraph, or a heading drawn at another size, would slip past the list above.
        var view = cut.Find(".munin-explorer-kilde").TextContent;

        Assert.DoesNotContain("Kriterier for tilgang til data", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Priser", view, StringComparison.Ordinal);
        Assert.DoesNotContain("publiserte variabler i denne kilden", view, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedCore_WhenItIsGivenKeldasParametersAndNoSections_ThenItDrawsOnlyItsOwnBlocks()
    {
        // THE OTHER HALF OF THE TRAP, and the one the two renders above cannot see between them.
        // They render the two explorers, so they catch a section moved into the core only if it
        // was moved unconditionally. DataCollectionsHeading is the one parameter Kelda passes and
        // Runa does not, which makes it a ready-made "am I Kelda?" switch inside the core: the
        // three sections put inside KildeView behind `@if (DataCollectionsHeading is not null)`
        // pass Kelda's test (same sections, same order, same level) and pass Runa's (Runa passes no
        // such heading), and the seam is gone with the whole suite green.
        //
        // So this renders the core directly, with Kelda's parameter set and none of Kelda's markup.
        // That is not the restatement OpenInRuna refuses to be: this is a claim about the core —
        // that its own blocks are the whole of what it draws — rather than a second copy of what an
        // explorer is believed to pass. The only thing that makes these headings appear is markup
        // inside KildeView, which is exactly what must not be there.
        var cut = Render<KildeView>(b => b
            .Add(c => c.Kilde, Tromso())
            .Add(c => c.Language, (string?)null)
            .Add(c => c.HeadingLevel, 3)
            .Add(c => c.DataCollectionsHeading, "Delkilder og datasamlinger"));

        Assert.Equal(
        [
            "Metadata",
            "Delkilder og datasamlinger",
            "Kildeinformasjon",
            "Statistikk",
        ], TextOf(cut.FindAll(BlockHeadings)));
    }

    [Fact]
    public void Kelda_WhenTheHostAsksForEnglish_ThenEachSectionsBodySitsUnderItsOwnHeading()
    {
        // The six strings this view added are all Kelda's, and nothing else in the suite renders an
        // open kilde in English — the list-chrome English test never opens one. That leaves the
        // failure a positional record makes silent: HeadingAccessCriteria, HeadingPrices and
        // HeadingVariables are three adjacent strings, BodyAccessCriteria and BodyPrices two more,
        // and transposing any pair at the English construction site compiles and passes
        // LanguageTest, which only asks whether each member is non-empty. The English page would
        // then ship with "Prices" over the access-criteria sentence, unseen by anyone reading
        // Norwegian.
        //
        // Asserted pairwise — each body read out of the paragraph that follows its own heading —
        // rather than as two Contains over the markup, because a Contains for each string passes
        // just as happily when the two have swapped places.
        var cut = OpenInKelda(Tromso(), language: "en");

        Assert.Equal(
        [
            "Metadata",
            "Sub-sources and data collections",
            "Variables",
            "Criteria for access to data",
            "Prices",
            "Source information",
            "Statistics",
        ], TextOf(cut.FindAll(BlockHeadings)));

        Assert.Equal(
            "5752 published variables in this source.",
            BodyUnder(cut, "Variables"));
        Assert.StartsWith(
            "The criteria for access to data from this source",
            BodyUnder(cut, "Criteria for access to data"),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "The prices for having data from this source",
            BodyUnder(cut, "Prices"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The text of the paragraph immediately after the block heading reading
    /// <paramref name="heading"/>.
    /// </summary>
    /// <remarks>
    /// Walks from the heading rather than indexing the paragraphs, because that is what makes the
    /// assertion pairwise: it can only answer with the body of the section it was asked about, so
    /// two bodies that swapped places fail rather than both still being somewhere on the page.
    /// </remarks>
    private static string BodyUnder(IRenderedComponent<KildeExplorer> cut, string heading)
    {
        var block = cut.FindAll(BlockHeadings)
                       .First(e => string.Equals(e.TextContent.Trim(), heading, StringComparison.Ordinal));

        var body = block.NextElementSibling
            ?? throw new InvalidOperationException($"Nothing follows the \"{heading}\" heading.");

        Assert.Equal("P", body.TagName);

        return body.TextContent.Trim();
    }

    [Theory]
    [InlineData(null, "H4")]
    [InlineData(2, "H4")]
    [InlineData(4, "H6")]
    public void Kelda_WhenTheHostMountsItAtALevel_ThenItsSectionsSitLevelWithTheCoresOwnBlocks(
        int? headingLevel,
        string expected)
    {
        // The level the core gives its own blocks is private to it, so Kelda mirrors the arithmetic
        // rather than reading it — and an arithmetic mirrored in two places is one that drifts
        // silently. A section one level too deep is not a cosmetic difference: it claims "Variabler"
        // is a part of the datasamlinger above it to everyone navigating the page by heading.
        //
        // The last row is the flattening rather than an off-by-one: a title at h4 puts the kilde at
        // h5 and every block at h6, which is where both sides stop.
        var cut = OpenInKelda(Tromso(), headingLevel);

        var levels = cut.FindAll(BlockHeadings).Select(e => e.TagName).Distinct(StringComparer.Ordinal);

        Assert.Equal([expected], levels);
    }

    [Fact]
    public void Kelda_WhenEverySectionIsOnScreen_ThenEveryClassNameIsOneSomeStylesheetDefines()
    {
        // Both sample hosts style every name this package writes, so looking at one proves nothing
        // about a host that carries Stiler and nothing else — this guard is the only thing that
        // catches a name nobody has a rule for, and it has to be run over the render that has all
        // four sections open rather than over the list.
        //
        // Compared against an empty list rather than asserted empty, so a failure names the classes
        // instead of saying only that there were some.
        var cut = OpenInKelda(Tromso());

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    /// <summary>
    /// Razor comments, the only comment syntax in <c>KildeView.razor</c> — that file carries no
    /// <c>@code</c> block, so nothing in it is written the C# way.
    /// </summary>
    private static readonly Regex RazorComment = new(@"@\*.*?\*@", RegexOptions.Singleline);

    /// <summary>
    /// C# comments in <c>KildeView.razor.cs</c>, both <c>///</c> and <c>//</c> — anchored to the
    /// start of a line, and the anchor is the whole robustness of this guard.
    /// </summary>
    /// <remarks>
    /// A bare <c>//.*$</c> does not know a comment from a string literal. One
    /// <c>href="https://helsedata.no/priser"</c> — which is exactly where the two static blocks are
    /// headed under <c>Fhi.Metadata-ay3zz</c> — would blank the rest of that line before the word
    /// list ever saw it, and a hard-coded section sitting on it would report clean. The failure
    /// direction is a false pass on the one edit this exists to catch, which is the shape
    /// <see cref="HostClassNames"/> documents at length: a comment naming <c>.tag</c> made that
    /// check answer "styled" for the very name it was documenting as unstyled. Every comment in
    /// the guarded file opens its own line, so the anchor costs the check nothing.
    /// </remarks>
    private static readonly Regex LineComment = new(@"^[ \t]*//.*$", RegexOptions.Multiline);

    /// <summary>
    /// The tokens a Kelda section is written with, which is what the core has to be free of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifiers rather than the Norwegian words, because the Norwegian words cannot appear.
    /// Identifiers here are English and every display string lives in <see cref="Texts"/> behind
    /// one, so a section moved into the core compiles as
    /// <c>@Heading(BlockLevel, T.HeadingPrices, "headline headline-s")</c> — no "Priser", no
    /// "Variabler", no "Kriterier" anywhere in the file. A guard on the Norwegian would pass
    /// against a core that hardcodes all three of Kelda's sections, which is what it was doing.
    /// </para>
    /// <para>
    /// Each name is the shortest one that cannot hit something the core legitimately says.
    /// <c>AccessCriteria</c> and <c>Prices</c> catch both the heading and the body member of their
    /// block. <c>Variables</c> on its own would not do: the core says <c>T.FieldTotalVariables</c>
    /// in its own sidebar, so the section's own <c>HeadingVariables</c> is what is banned, and
    /// <c>KildeVariableCount</c> rather than <c>VariableCount</c> for the same reason — the
    /// datasamling table reads <c>row.VariableCount</c>.
    /// </para>
    /// <para>
    /// The Norwegian words stay on the list underneath them. They can no longer be written the
    /// idiomatic way, but a literal dropped into the markup in a hurry is still a leak, and it is
    /// the one a reader grepping for "why is Priser not allowed here" will find.
    /// </para>
    /// </remarks>
    private static readonly string[] KeldasOwnWords =
    [
        "HeadingVariables",
        "AccessCriteria",
        "Prices",
        "KildeVariableCount",
        "Kelda",
        "Priser",
        "Kriterier",
        "Variabler",
    ];

    [Theory]
    [InlineData("KildeView.razor")]
    [InlineData("KildeView.razor.cs")]
    public void SharedCore_WhenItsSourceIsRead_ThenItNamesNeitherExplorerNorTheirSections(string file)
    {
        // The same change caught from the side a render cannot see. A core that has learned the
        // word "Priser" is a core that has learned which explorer is calling it, and every later
        // difference between the two then costs another flag in the one component they share. It is
        // a one-off check that costs nothing and catches exactly the one edit that makes this
        // component undivided again.
        //
        // Comments are stripped first, with the one pattern that file kind actually uses, because
        // that file explains at length why it has no Kelda in it and a check that prose can break
        // is one that gets deleted the first time somebody documents the rule it enforces. One
        // pattern per kind rather than both over both: the razor strip is a no-op on the .cs half
        // and the line strip a no-op on the .razor half, and running each where it does nothing
        // hides which of them is holding the check up.
        //
        // What is left is code — including every reference to Texts, which is where a hard-coded
        // section's words actually live.
        var source = File.ReadAllText(Repo.In("src", "Fhi.Munin.Explorer", "Blazor", file));

        var code = file.EndsWith(".cs", StringComparison.Ordinal)
            ? LineComment.Replace(source, " ")
            : RazorComment.Replace(source, " ");

        foreach (var word in KeldasOwnWords)
        {
            Assert.DoesNotContain(word, code, StringComparison.Ordinal);
        }
    }
}
