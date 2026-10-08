using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// One result row per (variable, datasamling): "Operasjonsdato for primærtumor" from Lungekreft
/// and from Livmorhals are two database fields to a researcher, so they render, open and save as
/// two rows sharing one variable id (Fhi.Metadata-d07al.1, acceptance A6).
/// </summary>
public class RowsPerDatasamlingTest : ExplorerTestContext
{
    private static readonly Guid ListId = Guid.NewGuid();
    private static readonly Guid Variable = new("bc8a6515-af36-44d0-aa10-b498813327b4");
    private static readonly Guid Lungekreft = new("0c027ce6-2994-45bf-99ec-facad3d1703e");
    private static readonly Guid Livmorhals = new("4411873c-0367-4334-a7cf-8e763c8e3490");

    private static VariableSummary Row(Guid datasamling, string datasamlingName, int fromYear) => new()
    {
        Id = Variable,
        RowKey = $"{Variable}:{datasamling}",
        Code = "V_KREG.S_DATOOPRPRIMAR",
        PreferredTerm = "Operasjonsdato for primærtumor",
        KildeName = "Kreftregisteret",
        DatasamlingId = datasamling,
        DatasamlingName = datasamlingName,
        DataFrom = new DateTimeOffset(fromYear, 1, 1, 0, 0, 0, TimeSpan.Zero),
    };

    private static readonly VariableSummary LungekreftRow = Row(Lungekreft, "Nasjonalt kvalitetsregister for lungekreft", 2002);
    private static readonly VariableSummary LivmorhalsRow = Row(Livmorhals, "Livmorhalsregisteret", 2007);

    private sealed class RowClient : EmptyMuninExplorerClient
    {
        public List<(Guid Variable, Guid? Datasamling)> DetailsAskedFor { get; } = [];
        public List<IReadOnlyCollection<VariableDatasamlingKey>> ItemsAdded { get; } = [];
        public List<IReadOnlyCollection<VariableDatasamlingKey>> ItemsRemoved { get; } = [];
        public int IdsCalls { get; private set; }
        public int LegacyAddCalls { get; private set; }
        public HashSet<VariableDatasamlingKey> Stored { get; } = [];

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [LungekreftRow with { }, LivmorhalsRow with { }],
                TotalCount = 2,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            DetailsAskedFor.Add((id, null));
            return Task.FromResult<VariableDetail?>(Detail(id, null));
        }

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical, Guid? datasamlingId, CancellationToken cancellationToken = default)
        {
            DetailsAskedFor.Add((id, datasamlingId));
            return Task.FromResult<VariableDetail?>(Detail(id, datasamlingId));
        }

        // Only Lungekreft's statistikk is attributed to it, by its code segment (design 1a).
        private static VariableDetail Detail(Guid id, Guid? datasamlingId) => new()
        {
            Id = id,
            Code = "V_KREG.S_DATOOPRPRIMAR",
            PreferredTerm = "Operasjonsdato for primærtumor",
            DatasamlingId = datasamlingId,
            Statistics = datasamlingId == Lungekreft
                ? [new() { AdditionalProperties = new Dictionary<string, string?> { ["SisteOppdaterteAarssett"] = "2024", ["MIN"] = "1" } }]
                : [],
        };

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>([new VariableList { Id = ListId, Name = "Kreft" }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. Stored.Select(k => new VariableListItem { VariableId = k.VariableId, DatasamlingId = k.DatasamlingId, ItemId = Guid.NewGuid() })],
                TotalCount = Stored.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<VariableRowSet> GetVariableRowsAsync(
            string? search, VariableFilter? filter = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VariableRowSet
            {
                Rows = [VariableDatasamlingKey.Of(LungekreftRow), VariableDatasamlingKey.Of(LivmorhalsRow)],
                MaxRows = 2000,
            });

        public override Task<bool> AddItemsToMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
            ItemsAdded.Add(items);
            Stored.UnionWith(items);
            return Task.FromResult(true);
        }

        public override Task<bool> RemoveItemsFromMyListAsync(
            Guid id, IReadOnlyCollection<VariableDatasamlingKey> items, CancellationToken cancellationToken = default)
        {
            ItemsRemoved.Add(items);
            Stored.ExceptWith(items);
            return Task.FromResult(true);
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            LegacyAddCalls++;
            return Task.FromResult(true);
        }

        public override Task<VariableIdSet> GetVariableIdsAsync(
            string? search, VariableFilter? filter = null, CancellationToken cancellationToken = default)
        {
            IdsCalls++;
            return Task.FromResult(new VariableIdSet { Ids = [Variable], MaxIds = 2000 });
        }
    }

    private IRenderedComponent<VariableSearch> RenderRows(
        RowClient client, bool signedIn = true, Action<ComponentParameterCollectionBuilder<VariableSearch>>? parameters = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        return Render<VariableSearch>(p =>
        {
            p.Add(c => c.IsAuthenticated, signedIn);
            parameters?.Invoke(p);
        });
    }

    private static IReadOnlyList<IElement> NameButtons(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name");

    private static void Open(IRenderedComponent<VariableSearch> cut, int row) =>
        NameButtons(cut)[row].Click(new MouseEventArgs { Detail = 1 });

    private static IElement SaveButton(IRenderedComponent<VariableSearch> cut) =>
        cut.Find(".munin-explorer-detail button[aria-pressed]");

    [Fact]
    public void Render_WhenOneVariableHasTwoDatasamlinger_ThenTwoRowsWithTheirOwnIdsAndDatasamling()
    {
        // @key on the variable id alone threw on the duplicate; the DOM ids collided as well.
        var cut = RenderRows(new RowClient());

        Assert.Equal(2, NameButtons(cut).Count);
        Assert.Equal(2, NameButtons(cut).Select(b => b.QuerySelector("[id]")?.Id ?? b.Id).Distinct().Count());
        Assert.Equal(
            ["Nasjonalt kvalitetsregister for lungekreft", "Livmorhalsregisteret"],
            cut.FindAll(".munin-explorer-dataitem-main__dataCollection .munin-explorer-dataitem-main__column__text")
                .Select(cell => cell.TextContent));
    }

    [Fact]
    public void Open_WhenTheSecondRowIsOpened_ThenOnlyItOpensAndItsDetailIsAskedForFromItsDatasamling()
    {
        var client = new RowClient();
        var reported = new List<(string Which, Guid? Value)>();
        var cut = RenderRows(client, parameters: p => p
            .Add(c => c.SelectedDatasamlingIdChanged, (Guid? d) => reported.Add(("datasamling", d)))
            .Add(c => c.SelectedVariableIdChanged, (Guid? v) => reported.Add(("variable", v))));

        Open(cut, 1);

        Assert.Equal(["false", "true"], NameButtons(cut).Select(b => b.GetAttribute("aria-expanded")));
        Assert.Equal((Variable, (Guid?)Livmorhals), Assert.Single(client.DetailsAskedFor));
        Assert.Equal([("datasamling", (Guid?)Livmorhals), ("variable", Variable)], reported);
    }

    [Fact]
    public void DataTab_WhenEachRowIsOpened_ThenOnlyTheLungekreftRowShowsStatistics()
    {
        // Statistics follow the row's own datasamling, from the detail asked for with it; no list column carries them.
        var cut = RenderRows(new RowClient(), signedIn: false);

        Assert.Empty(cut.FindAll("[class*='statistikk'], [class*='statistics']"));

        Open(cut, 0);
        Assert.DoesNotContain("Ingen kodeverk eller statistikk registrert", cut.Find(".munin-explorer-detail").TextContent);

        Open(cut, 1);
        Assert.Contains("Ingen kodeverk eller statistikk registrert", cut.Find(".munin-explorer-detail").TextContent);
    }

    [Fact]
    public void Save_WhenTheLungekreftRowIsSaved_ThenThePairIsSavedAndTheLivmorhalsRowStaysUnsaved()
    {
        var client = new RowClient();
        var cut = RenderRows(client);

        Open(cut, 0);
        SaveButton(cut).Click();

        Assert.Equal([new VariableDatasamlingKey(Variable, Lungekreft)], Assert.Single(client.ItemsAdded));
        Assert.Equal(0, client.LegacyAddCalls);
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));

        Open(cut, 1);

        Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Save_WhenASavedRowIsPressedAgain_ThenOnlyThatPairIsRemoved()
    {
        var client = new RowClient();
        client.Stored.Add(new VariableDatasamlingKey(Variable, Lungekreft));
        client.Stored.Add(new VariableDatasamlingKey(Variable, Livmorhals));
        var cut = RenderRows(client);

        Open(cut, 1);
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
        SaveButton(cut).Click();

        Assert.Equal([new VariableDatasamlingKey(Variable, Livmorhals)], Assert.Single(client.ItemsRemoved));
        Open(cut, 0);
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void SaveAll_WhenPressed_ThenEveryRowIsSavedAsItsPair()
    {
        // 564 rows for Kreftregisteret, not its 431 variables: the rows route, not the ids.
        var client = new RowClient();
        var cut = RenderRows(client);

        cut.Find("[id^='munin-explorer-save-all-']").Click();

        Assert.Equal(
            [new VariableDatasamlingKey(Variable, Lungekreft), new VariableDatasamlingKey(Variable, Livmorhals)],
            Assert.Single(client.ItemsAdded));
        Assert.Equal(0, client.IdsCalls);
        Assert.Equal(0, client.LegacyAddCalls);
    }

    [Fact]
    public void Summary_WhenTwoRowsShareAVariable_ThenTheCountIsRowsInTheSameWords()
    {
        // Inge: a row is the variable as delivered from one datasamling, so "variabler" stays.
        var cut = RenderRows(new RowClient(), signedIn: false);

        Assert.StartsWith("2 variabler funnet", cut.Find("p[role='status']").TextContent.Trim());
    }

    [Fact]
    public void DeepLink_WhenTheLinkNamesTheDatasamling_ThenThatRowOpens()
    {
        var client = new RowClient();
        var cut = RenderRows(client, signedIn: false, p => p
            .Add(c => c.SelectedVariableId, Variable)
            .Add(c => c.SelectedDatasamlingId, Livmorhals));

        cut.WaitForAssertion(() =>
            Assert.Equal(["false", "true"], NameButtons(cut).Select(b => b.GetAttribute("aria-expanded"))));
        Assert.Equal((Variable, (Guid?)Livmorhals), Assert.Single(client.DetailsAskedFor));
    }

    [Fact]
    public void DeepLink_WhenAnOlderLinkNamesOnlyTheVariable_ThenItsFirstRowOpensAndTheDatasamlingIsReported()
    {
        var client = new RowClient();
        Guid? reported = null;
        var cut = RenderRows(client, signedIn: false, p => p
            .Add(c => c.SelectedVariableId, Variable)
            .Add(c => c.SelectedDatasamlingIdChanged, (Guid? d) => reported = d));

        cut.WaitForAssertion(() =>
            Assert.Equal(["true", "false"], NameButtons(cut).Select(b => b.GetAttribute("aria-expanded"))));
        Assert.Equal(Lungekreft, reported);
        Assert.Equal((Variable, (Guid?)Lungekreft), Assert.Single(client.DetailsAskedFor));
    }

    [Fact]
    public void DeepLink_WhenTheDatasamlingHasNoRowOnThePage_ThenNothingOpens()
    {
        Guid? reported = Variable;
        var cut = RenderRows(new RowClient(), signedIn: false, p => p
            .Add(c => c.SelectedVariableId, Variable)
            .Add(c => c.SelectedDatasamlingId, Guid.NewGuid())
            .Add(c => c.SelectedVariableIdChanged, (Guid? v) => reported = v));

        cut.WaitForAssertion(() => Assert.Null(reported));
        Assert.All(NameButtons(cut), b => Assert.Equal("false", b.GetAttribute("aria-expanded")));
    }
}
