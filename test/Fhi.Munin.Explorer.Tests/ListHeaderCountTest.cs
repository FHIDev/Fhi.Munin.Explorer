using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// The saved list's header counts variables, not rows: my/lists counts rows, and one variable from two
// datasamlinger is two of them, so «3 variabler» stood over a list of two (Fhi.Metadata-t7kx8).
public sealed class ListHeaderCountTest : ExplorerTestContext
{
    private static readonly Guid Als = Guid.NewGuid();
    private static readonly Guid Tromso = Guid.NewGuid();
    private static readonly Guid Inklusjon = Guid.NewGuid();
    private static readonly Guid Oppfolging = Guid.NewGuid();

    private sealed class ListClient(params VariableListItem[] items) : EmptyMuninExplorerClient
    {
        private static readonly Guid ListId = Guid.NewGuid();

        // Changeable, as another tab changes the list.
        public List<VariableListItem> Rows { get; } = [.. items];

        // As Munin answers: variableCount is the stored rows.
        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [
                    new VariableList { Id = ListId, Name = "Mine", VariableCount = Rows.Count },
                    new VariableList { Id = Guid.NewGuid(), Name = "Andre", VariableCount = 0 },
                ]);

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = id == ListId ? [.. Rows] : [],
                TotalCount = id == ListId ? Rows.Count : 0,
                PageNumber = page,
                Size = pageSize,
                TotalPages = 1,
            });
    }

    private static VariableListItem Row(Guid variable, Guid kilde, Guid datasamling) => new()
    {
        VariableId = variable,
        AddedAt = DateTimeOffset.UtcNow,
        VariableName = "Alder",
        VariableCode = "V_ALS.ALDER",
        KildeId = kilde,
        KildeName = kilde == Als ? "Als registeret" : "Tromsøundersøkelsen",
        DatasamlingId = datasamling,
        DatasamlingName = datasamling == Inklusjon ? "Inklusjon" : "Oppfølging",
        DataType = "2",
    };

    private IRenderedComponent<VariableListView> Mount(string language, params VariableListItem[] rows)
    {
        Services.AddSingleton<IMuninExplorerClient>(new ListClient(rows));
        Services.AddScoped<VariableListState>();

        return Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true).Add(c => c.Language, language));
    }

    private string Header(string language, params VariableListItem[] rows) =>
        Mount(language, rows).Find("p.caption").TextContent;

    [Theory]
    [InlineData("no", "2 variabler (3 rader) · fra 2 datakilder")]
    [InlineData("en", "2 variables (3 rows) · from 2 sources")]
    public void Header_WhenAVariableIsInTheListFromTwoDatasamlinger_ThenItCountsVariablesAndSaysTheRows(
        string language, string expected)
    {
        var alder = Guid.NewGuid();

        var header = Header(language,
            Row(alder, Als, Inklusjon), Row(alder, Als, Oppfolging), Row(Guid.NewGuid(), Tromso, Inklusjon));

        Assert.StartsWith(expected, header, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("no", "1 variabel (2 rader) · fra 1 datakilde")]
    [InlineData("en", "1 variable (2 rows) · from 1 source")]
    public void Header_WhenTheOneVariableIsInTwoRows_ThenTheVariableIsSingularAndTheRowsPlural(
        string language, string expected)
    {
        var alder = Guid.NewGuid();

        var header = Header(language, Row(alder, Als, Inklusjon), Row(alder, Als, Oppfolging));

        Assert.StartsWith(expected, header, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("no", "2 variabler · fra 2 datakilder")]
    [InlineData("en", "2 variables · from 2 sources")]
    public void Header_WhenEveryVariableIsOneRow_ThenTheRowsAreNotMentioned(string language, string expected)
    {
        var header = Header(language, Row(Guid.NewGuid(), Als, Inklusjon), Row(Guid.NewGuid(), Tromso, Inklusjon));

        Assert.StartsWith(expected, header, StringComparison.Ordinal);
        Assert.DoesNotContain("(", header, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("no", "1 variabel · fra 1 datakilde")]
    [InlineData("en", "1 variable · from 1 source")]
    public void Header_WhenTheListHoldsOneVariableInOneRow_ThenEveryCountIsSingular(string language, string expected)
    {
        Assert.StartsWith(expected, Header(language, Row(Guid.NewGuid(), Als, Inklusjon)), StringComparison.Ordinal);
    }

    [Fact]
    public void Picker_WhenTheListOnScreenHasMoreRowsThanVariables_ThenItsOptionCountsVariablesAsTheHeaderDoes()
    {
        // The option sits right above the header; «Mine (3 variabler)» over «2 variabler (3 rader)» is the bead again.
        var alder = Guid.NewGuid();

        var cut = Mount("no", Row(alder, Als, Inklusjon), Row(alder, Als, Oppfolging), Row(Guid.NewGuid(), Tromso, Inklusjon));

        Assert.Equal("Mine (2 variabler)", cut.Find("option[selected]").TextContent.Trim());
    }

    [Fact]
    public async Task Header_WhenOneOfAVariablesRowsIsRemovedByVariable_ThenItsVariableAndRowsGoTogether()
    {
        // Removing a variable takes every row of it, so the count follows the walk's set live.
        var alder = Guid.NewGuid();
        var cut = Mount("no", Row(alder, Als, Inklusjon), Row(alder, Als, Oppfolging), Row(Guid.NewGuid(), Tromso, Inklusjon));
        var state = Services.GetRequiredService<VariableListState>();

        await cut.InvokeAsync(() => state.RemoveVariablesAsync(state.ActiveListId!.Value, [alder]));

        cut.WaitForAssertion(() => Assert.StartsWith("1 variabel ·", cut.Find("p.caption").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void Texts_WhenRowsAreCounted_ThenBothLanguagesHandleTheSingularOfEach()
    {
        Assert.Equal("1 variabel (2 rader)", Texts.For("no").ListVariablesInRows(1, 2));
        Assert.Equal("3 variabler (1 rad)", Texts.For("no").ListVariablesInRows(3, 1));
        Assert.Equal("1 variable (2 rows)", Texts.For("en").ListVariablesInRows(1, 2));
        Assert.Equal("3 variables (1 row)", Texts.For("en").ListVariablesInRows(3, 1));
    }

    [Fact]
    public async Task Header_WhenAnotherTabAddsAVariableAndTheListsAreRefreshed_ThenTheCountsFollow()
    {
        // A refresh reads my/lists again; the counts drawn from the list's own walk must not keep the old answer.
        var alder = Guid.NewGuid();
        var client = new ListClient(Row(alder, Als, Inklusjon), Row(alder, Als, Oppfolging), Row(Guid.NewGuid(), Tromso, Inklusjon));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true).Add(c => c.Language, "no"));
        cut.WaitForAssertion(() => Assert.StartsWith("2 variabler (3 rader)", cut.Find("p.caption").TextContent, StringComparison.Ordinal));
        var state = Services.GetRequiredService<VariableListState>();

        client.Rows.Add(Row(Guid.NewGuid(), Tromso, Oppfolging));
        await cut.InvokeAsync(() => state.RefreshAsync());

        cut.WaitForAssertion(() => Assert.StartsWith("3 variabler (4 rader)", cut.Find("p.caption").TextContent, StringComparison.Ordinal));
        Assert.Equal("Mine (3 variabler)", cut.Find("option[selected]").TextContent.Trim());
    }
}
