using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The open row of an own list leads on to its kilde, datasamling and variabelgrupper (ADO 121586).</summary>
public class ListRowPanelOwnersTest : ExplorerTestContext
{
    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid KildeId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid DatasamlingId = new("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid GroupId = new("eeeeeeee-0000-0000-0000-000000000001");

    private static readonly VariableListItem Age = new()
    {
        VariableId = new Guid("aaaaaaaa-0000-0000-0000-000000000002"),
        VariableName = "Alder",
        VariableCode = "V_HKR.ALDER",
    };

    private sealed class OwnersClient : EmptyMuninExplorerClient
    {
        public bool WithOwners { get; init; } = true;

        public List<Guid> KilderAskedFor { get; } = [];

        public List<Guid> DatasamlingerAskedFor { get; } = [];

        public List<(string? Search, VariableFilter? Filter)> Searches { get; } = [];

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = ListId, Name = "Hjertelista", VariableCount = 1 }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [Age], TotalCount = 1, PageNumber = 1, Size = pageSize, TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(new VariableDetail
            {
                Id = id,
                Code = "V_HKR.ALDER",
                PreferredTerm = "Alder",
                Description = "Om alder.",
                KildeId = WithOwners ? KildeId : Guid.Empty,
                KildeName = "Hjerte- og karregisteret",
                DatasamlingId = WithOwners ? DatasamlingId : null,
                DatasamlingName = "Alle variabler",
                AllVariabelgrupper = [new VariabelgruppeReference { Id = GroupId, Name = "Demografi" }],
            });

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default)
        {
            KilderAskedFor.Add(id);
            return Task.FromResult<KildeDetail?>(new KildeDetail { Id = id, Code = "K_HKR", PreferredTerm = "Hjerte- og karregisteret" });
        }

        public override Task<DatasamlingDetail?> GetDatasamlingAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DatasamlingerAskedFor.Add(id);
            return Task.FromResult<DatasamlingDetail?>(new DatasamlingDetail
            {
                Id = id, Code = "D_HKR", PreferredTerm = "Alle variabler", ParentKildeId = KildeId,
            });
        }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Searches.Add((search, filter));
            return Task.FromResult(new Page<VariableSummary> { Items = [], TotalCount = 0, PageNumber = 1, Size = pageSize, TotalPages = 0 });
        }
    }

    private IRenderedComponent<VariableListView> OpenRow(OwnersClient client)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForElement("th[scope=row] button").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        return cut;
    }

    private static void Press(IRenderedComponent<VariableListView> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    [Fact]
    public void VisDatakilde_OpensTheKildeInPlaceOfTheList_AndTheWayBackFindsTheRowStillOpen()
    {
        var client = new OwnersClient();
        var cut = OpenRow(client);

        Press(cut, "Vis datakilde");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal([KildeId], client.KilderAskedFor);
            Assert.Contains("Hjerte- og karregisteret", cut.Find("[id^='munin-explorer-list-source-']").TextContent);
            Assert.Empty(cut.FindAll("th[scope=row]"));
            LastFocus().ShouldBeElementReferenceTo(cut.Find("[role=region][id^='munin-explorer-list-source-']"));
        });

        Press(cut, "← Tilbake til variabler");

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[id^='munin-explorer-list-source-']"));
            Assert.Single(cut.FindAll("[role=region][id^='munin-explorer-list-panel-']"));
            LastFocus().ShouldBeElementReferenceTo(cut.FindAll("button").Single(b => b.TextContent.Trim() == "Vis datakilde"));
        });
    }

    private object? LastFocus() => JSInterop.Invocations["Blazor._internal.domWrapper.focus"][^1].Arguments[0];

    [Fact]
    public void VisDatasamling_OpensTheDatasamling_AndItsKildeFromThere()
    {
        var client = new OwnersClient();
        var cut = OpenRow(client);

        Press(cut, "Vis datasamling");
        cut.WaitForAssertion(() => Assert.Equal([DatasamlingId], client.DatasamlingerAskedFor));

        Press(cut, "Vis datakilden");
        cut.WaitForAssertion(() => Assert.Equal([KildeId], client.KilderAskedFor));
    }

    [Fact]
    public void AVariableThatNamesNoOwner_OffersNeitherButton()
    {
        var cut = OpenRow(new OwnersClient { WithOwners = false });

        var labels = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.DoesNotContain("Vis datakilde", labels);
        Assert.DoesNotContain("Vis datasamling", labels);
    }

    [Fact]
    public void AListStandingAlone_NamesItsGroupsAsWords()
    {
        var cut = OpenRow(new OwnersClient());

        Assert.Empty(cut.FindAll("button[aria-label^='Vis variablene i variabelgruppen']"));
        Assert.Contains("Demografi", cut.Find("[role=region][id^='munin-explorer-list-panel-']").TextContent);
    }

    [Fact]
    public void InTheExplorer_AGroupOpensTheSearchNarrowedToIt_WithNothingElseLeftOver()
    {
        var client = new OwnersClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        this.SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/variabler?search=hjerte&kildeIds={KildeId}");

        var cut = Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, true));
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").Click();
        cut.WaitForElement("th[scope=row] button").Click();

        cut.WaitForElement("button[aria-label='Vis variablene i variabelgruppen Demografi']").Click();

        cut.WaitForAssertion(() =>
        {
            var (search, filter) = client.Searches[^1];
            Assert.Null(search);
            Assert.Equal(VariableFilter.None with { VariabelgruppeIds = [GroupId] }, filter);
            Assert.Equal("true", cut.FindAll("[role=tab]")
                .Single(t => t.TextContent.Trim() == "Søkeresultat").GetAttribute("aria-selected"));
        });
    }
}
