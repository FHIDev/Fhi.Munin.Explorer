using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// Stiler scopes the heading's rule under .munin-explorer-meta, so its place is as load-bearing as
// its class: anywhere else it renders as a browser-default h3. (Fhi.Metadata-yaco2)
public class DrawerHeadingTest : ExplorerTestContext
{
    private const string Heading = "munin-explorer-meta__heading";

    private const string Chevron = "button.munin-explorer-dataitem__expand-toggle";

    private const string LongName =
        "Antall_tidligere_fødsler_og_dødfødsler_etter_22_fullgåtte_uker_uten_et_eneste_mellomrom";

    private static readonly Guid TaleId = Guid.NewGuid();

    private static readonly Guid BlankId = Guid.NewGuid();

    private static readonly Guid LongId = Guid.NewGuid();

    private sealed class RowsClient(bool answer = true) : EmptyMuninExplorerClient
    {
        private static readonly VariableSummary[] Rows =
        [
            new() { Id = TaleId, Code = "V_ALS.TALE", PreferredTerm = "1. Tale" },
            new() { Id = BlankId, Code = "V_ALS.BLANK", PreferredTerm = "" },
            new() { Id = LongId, Code = "V_MFR.LANG", PreferredTerm = LongName },
        ];

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = Rows,
                TotalCount = Rows.Length,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            if (!answer)
            {
                return new TaskCompletionSource<VariableDetail?>().Task;
            }

            var row = Rows.Single(r => r.Id == id);

            return Task.FromResult<VariableDetail?>(
                new VariableDetail { Id = id, Code = row.Code, PreferredTerm = row.PreferredTerm });
        }
    }

    private sealed class KodeverkClient : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [new() { Id = TaleId, Code = "V_ALS.TALE", PreferredTerm = "1. Tale" }],
                TotalCount = 1,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(new VariableDetail
            {
                Id = id,
                Code = "V_ALS.TALE",
                PreferredTerm = "1. Tale",
                KodeverkLinks = [new() { KodeverkType = "kildekodeverk", KodeverkReference = "ALS_TALE", DisplayName = "ALS tale" }],
            });
    }

    private IRenderedComponent<VariableSearch> Render(
        Action<ComponentParameterCollectionBuilder<VariableSearch>>? parameters = null,
        bool answer = true)
    {
        Services.AddSingleton<IMuninExplorerClient>(new RowsClient(answer));

        return Render<VariableSearch>(parameters ?? (_ => { }));
    }

    private static IElement Open(IRenderedComponent<VariableSearch> cut, int row)
    {
        cut.FindAll($"ul.munin-explorer-data-list {Chevron}")[row].Click();

        return cut.Find(".munin-explorer-meta.munin-explorer-detail");
    }

    [Fact]
    public void Panel_WhenOpened_ThenItOpensWithTheNameAsAnH3UnderTheTitlesH2()
    {
        var cut = Render();

        var panel = Open(cut, 0);
        var heading = panel.FirstElementChild!;

        Assert.Equal("H2", cut.Find($"#{cut.Find("section.munin-explorer").GetAttribute("aria-labelledby")}").TagName);
        Assert.Equal("H3", heading.TagName);
        Assert.Equal(Heading, heading.ClassName);
        Assert.False(string.IsNullOrEmpty(heading.Id));
        Assert.Equal("1. Tale", heading.TextContent);
        Assert.Single(cut.FindAll($".{Heading}"));
    }

    // The Data tab's group headings sit under the drawer heading, not beside it.
    [Fact]
    public void Panel_WhenTheDataTabHasKodeverk_ThenItsGroupHeadingsNestUnderTheDrawerHeading()
    {
        Services.AddSingleton<IMuninExplorerClient>(new KodeverkClient());
        var cut = Render<VariableSearch>();

        var panel = Open(cut, 0);
        var group = panel.QuerySelector("[role=tabpanel] .munin-explorer-group")!;

        Assert.Equal("H3", panel.QuerySelector($".{Heading}")!.TagName);
        Assert.Equal("H4", group.TagName);
    }

    [Fact]
    public void Panel_WhenTheHostMountsTheTitleAtH3_ThenTheHeadingFollowsToH4()
    {
        var cut = Render(b => b.Add(c => c.HeadingLevel, 3));

        Assert.Equal("H4", Open(cut, 0).FirstElementChild!.TagName);
    }

    [Fact]
    public void Panel_WhenOpened_ThenTheRegionAndItsTablistAreNamedByTheHeadingOnce()
    {
        var cut = Render();

        var panel = Open(cut, 0);
        var heading = panel.QuerySelector($".{Heading}")!;

        Assert.Equal(heading.Id, panel.GetAttribute("aria-labelledby"));
        Assert.Equal(heading.Id, panel.QuerySelector("[role=tablist]")!.GetAttribute("aria-labelledby"));
        Assert.Equal("1. Tale", AccessibleName.Of(panel));
        Assert.Single(cut.FindAll($"#{heading.Id}"));
    }

    // The region's name must exist before the payload does, or a reader entering a loading panel
    // meets an unnamed landmark.
    [Fact]
    public void Panel_WhileTheDetailIsStillLoading_ThenTheHeadingIsAlreadyThere()
    {
        var cut = Render(answer: false);

        var panel = Open(cut, 0);

        Assert.Equal("true", panel.GetAttribute("aria-busy"));
        Assert.Equal("1. Tale", AccessibleName.Of(panel));
    }

    [Fact]
    public void Panel_WhenTheVariableHasNoName_ThenTheHeadingIsItsCode()
    {
        var cut = Render();

        var panel = Open(cut, 1);

        Assert.Equal("V_ALS.BLANK", panel.QuerySelector($".{Heading}")!.TextContent);
        Assert.Equal("V_ALS.BLANK", AccessibleName.Of(panel));
    }

    [Theory]
    [InlineData("no", null)]
    [InlineData("en", "no")]
    public void Panel_WhenTheReaderLanguageChanges_ThenTheNameStaysMarkedNorwegian(string language, string? lang)
    {
        var cut = Render(b => b.Add(c => c.Language, language));

        Assert.Equal(lang, Open(cut, 0).QuerySelector($".{Heading}")!.GetAttribute("lang"));
    }

    // Criterion 3: the chevron keeps its own name, its own sentence around the variable's name,
    // and keeps pointing at the panel; the heading must not become its label.
    [Theory]
    [InlineData("no", "Skjul detaljer for 1. Tale")]
    [InlineData("en", "Hide details for 1. Tale")]
    public void Chevron_WhenThePanelHasItsHeading_ThenItsNameAndControlsAreUnchanged(string language, string name)
    {
        var cut = Render(b => b.Add(c => c.Language, language));

        var panel = Open(cut, 0);
        var chevron = cut.FindAll($"ul.munin-explorer-data-list {Chevron}")[0];

        Assert.False(chevron.HasAttribute("aria-labelledby"));
        Assert.Equal(name, AccessibleName.Of(chevron));
        Assert.Equal(panel.Id, chevron.GetAttribute("aria-controls"));
        Assert.NotEqual(panel.QuerySelector($".{Heading}")!.Id, chevron.GetAttribute("aria-controls"));
    }

    // AC4's markup half: the unbroken name reaches the heading whole, inside the scoped panel,
    // which is what lets Stiler's overflow-wrap rule reach it. The geometry half is in
    // scripts/geometry-assertions.mjs.
    [Fact]
    public void Panel_WhenTheNameHasNoBreakInIt_ThenTheHeadingCarriesItWholeInsideThePanel()
    {
        var cut = Render();

        var panel = Open(cut, 2);
        var heading = panel.QuerySelector($".{Heading}")!;

        Assert.Equal(LongName, heading.TextContent);
        Assert.Equal(panel.Id, heading.Closest(".munin-explorer-meta")!.Id);
    }
}
