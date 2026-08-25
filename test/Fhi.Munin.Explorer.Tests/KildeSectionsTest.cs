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
/// The trap is in the second test rather than the first, and it is why both render the same kilde
/// out of the same fixture. An implementation that moved Kelda's three sections inside
/// <see cref="KildeView"/> behind a condition would pass every assertion about Kelda's own page and
/// would have taken down the separation the component is built to hold up; only a render of
/// <em>Runa's</em> view of the same source can say so. The source guard below catches the same
/// change from the other side, in the one place a rendered page cannot look: the core's own text.
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
    private IRenderedComponent<KildeExplorer> OpenInKelda(KildeDetail kilde, int? headingLevel = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(new KeldaClient(kilde));

        var cut = headingLevel is { } level
            ? Render<KildeExplorer>(b => b.Add(c => c.HeadingLevel, level))
            : Render<KildeExplorer>();

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
        Assert.Contains("5752 publiserte variabler i denne kilden.", cut.Markup);
        Assert.Contains("er beskrevet på helsedata.no.", cut.Markup);
    }

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
        // Comments are stripped first — both kinds — because that file explains at length why it
        // has no Kelda in it, and a check that prose can break is one that gets deleted the first
        // time somebody documents the rule it enforces. What is left is code, including the
        // user-facing strings, which is where a hard-coded section would land.
        var source = File.ReadAllText(Repo.In("src", "Fhi.Munin.Explorer", "Blazor", file));

        var code = Regex.Replace(source, @"@\*.*?\*@", " ", RegexOptions.Singleline);
        code = Regex.Replace(code, @"//.*$", " ", RegexOptions.Multiline);

        foreach (var word in new[] { "Kelda", "Priser", "Kriterier", "Variabler" })
        {
            Assert.DoesNotContain(word, code, StringComparison.Ordinal);
        }
    }
}
