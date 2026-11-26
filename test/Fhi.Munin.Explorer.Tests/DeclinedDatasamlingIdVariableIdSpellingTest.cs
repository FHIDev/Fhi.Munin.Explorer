using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

// With datasamlingId declined, ?variabelId= alone names the primary row, the same rule as the ?variabel= spelling
// (Fhi.Metadata-l9l2n.206).
public class DeclinedDatasamlingIdVariableIdSpellingTest : ExplorerTestContext
{
    private static readonly Guid VariableId = Guid.Parse("7a1f0c4e-2b8d-4e5a-9c3f-1d2e3f4a5b6c");
    private static readonly Guid KildeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid PrimaryId = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private static readonly Guid OtherId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string Code = "V_HKR.KJONN";

    private sealed class TwoRowClient(bool primaryFirst = true) : EmptyMuninExplorerClient, IMuninExplorerClient
    {
        private int _searches;

        public int Searches => Volatile.Read(ref _searches);

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _searches);
            return Task.FromResult(new Page<VariableSummary>
            {
                Items = primaryFirst
                    ? [Row(PrimaryId, "Hjerteinfarkt"), Row(OtherId, "Hjerneslag")]
                    : [Row(OtherId, "Hjerneslag"), Row(PrimaryId, "Hjerteinfarkt")],
                TotalCount = 2,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
        }

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(Detail());

        public Task<VariableDetail?> GetVariableByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(string.Equals(code, Code, StringComparison.OrdinalIgnoreCase) ? Detail() : null);

        private static VariableSummary Row(Guid datasamling, string name) =>
            new()
            {
                Id = VariableId,
                Code = Code,
                PreferredTerm = "Kjønn",
                KildeId = KildeId,
                KildeName = "Hjerte- og karregisteret",
                DatasamlingId = datasamling,
                DatasamlingName = name,
                RowKey = $"{VariableId}:{datasamling}",
                VersionStatus = "Active",
            };

        private static VariableDetail Detail() =>
            new()
            {
                Id = VariableId,
                Code = Code,
                PreferredTerm = "Kjønn",
                KildeId = KildeId,
                KildeName = "Hjerte- og karregisteret",
                KildeShortName = "HKR",
                DatasamlingId = PrimaryId,
                DatasamlingName = "Hjerteinfarkt",
                VersionStatus = "Active",
                AllDatasamlinger =
                [
                    new DatasamlingReference { Id = PrimaryId, Code = "K_HJERTEINFARKT" },
                    new DatasamlingReference { Id = OtherId, Code = "K_HJERNESLAG" },
                ],
            };
    }

    [Fact]
    public void Navigate_WhenANonPrimaryRowIsOpenAndTheHostLinksTheVariableIdAlone_ThenThePrimaryRowOpens()
    {
        var (cut, navigation, _) = RenderWithRowOpen("Hjerneslag", "datasamling=K_HJERNESLAG");

        navigation.NavigateTo($"http://localhost/variabler?search=kjonn&variabelId={VariableId}");

        cut.WaitForAssertion(() => Assert.Equal(["Hjerteinfarkt"], OpenRows(cut)));
    }

    [Fact]
    public void Navigate_WhenThePrimaryRowIsOpenAndTheHostLinksTheVariableIdAlone_ThenItIsTheSameViewWithNoNewSearch()
    {
        var (cut, navigation, client) = RenderWithRowOpen("Hjerteinfarkt", "datasamling=K_HJERTEINFARKT");
        var searches = client.Searches;

        navigation.NavigateTo($"http://localhost/variabler?search=kjonn&variabelId={VariableId}");

        cut.WaitForAssertion(() => Assert.Equal(["Hjerteinfarkt"], OpenRows(cut)));
        Assert.Equal(searches, client.Searches);
    }

    // With variabel declined too the explorer never learns the code, so the id link is its own address for the open
    // row and must keep it, as the changelog says.
    [Fact]
    public void Navigate_WhenVariabelIsAlsoDeclinedAndANonPrimaryRowIsOpen_ThenTheIdLinkKeepsThatRow()
    {
        var (cut, navigation, client) = RenderWithRowOpen("Hjerneslag", $"variabelId={VariableId}", ["datasamlingId", "variabel"]);
        var searches = client.Searches;

        navigation.NavigateTo($"http://localhost/variabler?search=kjonn&variabelId={VariableId}");

        cut.WaitForAssertion(() => Assert.Equal(["Hjerneslag"], OpenRows(cut)));
        Assert.Equal(searches, client.Searches);
    }

    // The primary pin is for a host that declines datasamlingId; elsewhere the variable alone still opens its first row.
    [Fact]
    public void Navigate_WhenDatasamlingIdIsNotDeclinedAndTheCodeIsKnown_ThenTheIdLinkOpensTheFirstListedRow()
    {
        var (cut, navigation, _) = RenderWithRowOpen("Hjerteinfarkt", "datasamling=K_HJERTEINFARKT", [], primaryFirst: false);

        navigation.NavigateTo($"http://localhost/variabler?search=kjonn&variabelId={VariableId}");

        cut.WaitForAssertion(() => Assert.Equal(["Hjerneslag"], OpenRows(cut)));
    }

    private (IRenderedComponent<VariableExplorer> Cut, NavigationManager Navigation, TwoRowClient Client) RenderWithRowOpen(
        string row, string written, string[]? declined = null, bool primaryFirst = true)
    {
        var client = new TwoRowClient(primaryFirst);
        var history = new HistoryRecorder(JSInterop.JSRuntime);
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddSingleton<IJSRuntime>(history);
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/variabler?search=kjonn");
        var cut = Render<VariableExplorer>(b => b.Add(c => c.DeclinedKeys, declined ?? ["datasamlingId"]));
        cut.WaitForAssertion(() => Assert.Equal(2, Names(cut).Count));
        Names(cut).Single(b => b.ParentElement!.ParentElement!.TextContent.Contains(row)).Click();
        cut.WaitForAssertion(() => Assert.Equal([row], OpenRows(cut)));
        cut.WaitForAssertion(() => Assert.Contains(written, history.Written.LastOrDefault() ?? ""));
        return (cut, navigation, client);
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> Names(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name");

    private static List<string> OpenRows(IRenderedComponent<VariableExplorer> cut) =>
        Names(cut)
            .Where(b => b.GetAttribute("aria-expanded") == "true")
            .Select(b => b.ParentElement!.ParentElement!.TextContent.Contains("Hjerteinfarkt") ? "Hjerteinfarkt" : "Hjerneslag")
            .ToList();
}
