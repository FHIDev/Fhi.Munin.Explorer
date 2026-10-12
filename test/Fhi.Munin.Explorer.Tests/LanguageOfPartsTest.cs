using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Which language a reader's screen reader speaks the component's own words in, when the host
/// page's <c>&lt;html lang&gt;</c> says something else (WCAG 3.1.1 and 3.1.2).
/// </summary>
/// <remarks>
/// helsedata's /en/ pages ship <c>&lt;html lang="no"&gt;</c>, so every word left unmarked was
/// announced in a Norwegian voice whatever <c>Language</c> said (Fhi.Metadata-nh7z2).
/// </remarks>
public class LanguageOfPartsTest : ExplorerTestContext
{
    private const string VariableName = "Alder ved diagnose";
    private const string VariableCode = "V_ABR.ALDER";
    private const string KildeName = "Abortregisteret";
    private const string DatasamlingName = "Abortregisteret Utlevering";
    private const string GroupName = "Bakgrunn";

    /// <summary>Every string the fixture's catalogue wrote: the only text allowed a Norwegian mark.</summary>
    private static readonly string[] CatalogueValues =
        [VariableName, VariableCode, KildeName, "ABR", DatasamlingName, GroupName];

    private sealed class OneRowClient : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items =
                [
                    new VariableSummary
                    {
                        Id = Guid.NewGuid(),
                        Code = VariableCode,
                        PreferredTerm = VariableName,
                        KildeId = Guid.NewGuid(),
                        KildeName = KildeName,
                        KildeShortName = "ABR",
                        DatasamlingId = Guid.NewGuid(),
                        DatasamlingName = DatasamlingName,
                        VariabelgruppeName = GroupName,
                    }
                ],
                TotalCount = 1,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });

        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
                [new KildeSummary { Id = Guid.NewGuid(), Code = "K_ABR", Name = KildeName }]);
    }

    private void Prepare()
    {
        Services.AddSingleton<IMuninExplorerClient>(new OneRowClient());
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
    }

    /// <summary>The explorer's title, found through the root's own label rather than its level.</summary>
    private static IElement Title(IElement root) =>
        root.QuerySelector($"#{root.GetAttribute("aria-labelledby")}")
        ?? throw new InvalidOperationException("The root names no title inside it.");

    [Theory]
    [InlineData("en", "en")]
    [InlineData("no", "no")]
    [InlineData("nb", "no")]
    [InlineData("en-GB", "en")]
    public void Root_WhenTheVariableSearchIsRendered_ThenItCarriesTheReadersLanguage(string language, string expected)
    {
        Prepare();

        var cut = Render<VariableSearch>(p => p.Add(c => c.Language, language));

        Assert.Equal(expected, cut.Find("[data-munin-explorer-version]").GetAttribute("lang"));
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("no", "no")]
    public void Root_WhenTheKildeSearchIsRendered_ThenItCarriesTheReadersLanguage(string language, string expected)
    {
        Prepare();

        var cut = Render<KildeSearch>(p => p.Add(c => c.Language, language));

        Assert.Equal(expected, cut.Find("[data-munin-explorer-version]").GetAttribute("lang"));
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("no", "no")]
    public void Root_WhenTheListViewIsMountedOnItsOwn_ThenItCarriesTheReadersLanguage(string language, string expected)
    {
        Prepare();

        var cut = Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, language));

        Assert.Equal(expected, cut.Find("[data-munin-explorer-version]").GetAttribute("lang"));
    }

    [Fact]
    public void Language_WhenTheReaderIsEnglish_ThenTheVariableSearchTitleIsNotUnderANorwegianMark()
    {
        Prepare();

        var cut = Render<VariableSearch>(p => p.Add(c => c.Language, "en"));

        Assert.Equal("en", Title(cut.Find("[data-munin-explorer-version]")).Closest("[lang]")!.GetAttribute("lang"));
    }

    [Fact]
    public void Language_WhenTheReaderIsEnglish_ThenTheKildeSearchTitleIsNotUnderANorwegianMark()
    {
        Prepare();

        var cut = Render<KildeSearch>(p => p.Add(c => c.Language, "en"));

        Assert.Equal("en", Title(cut.Find("[data-munin-explorer-version]")).Closest("[lang]")!.GetAttribute("lang"));
    }

    [Fact]
    public void Language_WhenTheReaderIsEnglish_ThenOnlyTheCataloguesOwnValuesAreMarkedNorwegian()
    {
        // The count half of the bead: 128 marks on helsedata's /en/ page, and the question is
        // which of them wrap our words. Every mark must hold one catalogue value and nothing else.
        Prepare();

        var cut = Render<VariableSearch>(p => p.Add(c => c.Language, "en"));
        cut.WaitForElement("ul.munin-explorer-data-list > li");

        var marked = cut.FindAll("[lang='no'], [lang='nb']");

        Assert.NotEmpty(marked);
        Assert.All(marked, element => Assert.Contains(element.TextContent.Trim(), CatalogueValues));
        Assert.Contains(marked, element => element.TextContent.Trim() == VariableName);
    }

    [Fact]
    public void Language_WhenTheReaderIsNorwegian_ThenNothingInsideIsMarkedEnglish()
    {
        // The sanity half: a Norwegian page whose catalogue holds no English has nothing to mark
        // as English, so a "en" anywhere would be our own chrome claiming the wrong language.
        Prepare();

        var cut = Render<VariableSearch>(p => p.Add(c => c.Language, "no"));
        cut.WaitForElement("ul.munin-explorer-data-list > li");

        var root = cut.Find("[data-munin-explorer-version]");

        Assert.Equal("no", root.GetAttribute("lang"));
        Assert.Empty(root.QuerySelectorAll("[lang='en']"));
    }
}
