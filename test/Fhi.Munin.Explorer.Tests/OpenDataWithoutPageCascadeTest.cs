using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A host words the open-data sentence on the explorer it mounts, and the kilde page that explorer opens has to carry it
/// (Fhi.Metadata-9huq2.54): deleting either explorer's cascade leaves every <see cref="KildeView"/> test green.
/// </summary>
public class OpenDataWithoutPageCascadeTest : ExplorerTestContext
{
    private const string HostText = "Statistikken fra {0} er åpen";
    private const string Expected = "Statistikken fra Testregisteret er åpen, men hjemmesiden er example.org";

    private static readonly Guid VariableId = new("aaaaaaaa-0000-0000-0000-0000000000a1");

    private static readonly KildeDetail Kilde = new()
    {
        Id = new("bbbbbbbb-0000-0000-0000-0000000000b1"),
        Code = "K_TEST",
        PreferredTerm = "Testregisteret",
        AdditionalProperties = new Dictionary<string, string?>
        {
            [AccessLevels.Key] = """["apne-data"]""",
            ["Hjemmeside"] = "https://www.example.org/test",
        },
    };

    private sealed class Client : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>([new KildeSummary { Id = Kilde.Id, Code = Kilde.Code, Name = Kilde.PreferredTerm, IsActive = true }]);

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(id == Kilde.Id ? Kilde : null);

        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(new KildeHierarchy { KildeId = id });

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [new VariableSummary { Id = VariableId, Code = "V_TEST.A", PreferredTerm = "A", KildeId = Kilde.Id, KildeName = Kilde.PreferredTerm }],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(new VariableDetail
            {
                Id = id,
                Code = "V_TEST.A",
                PreferredTerm = "A",
                KildeId = Kilde.Id,
                KildeName = Kilde.PreferredTerm,
            });
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private void Arrive(string url)
    {
        Services.AddSingleton<IMuninExplorerClient>(new Client());
        Services.AddScoped<VariableListState>();
        Services.AddSingleton<IJSRuntime>(new HistoryRecorder(JSInterop.JSRuntime));
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        Navigation.NavigateTo(url);
    }

    [Fact]
    public void KildeExplorer_WhenTheHostWordsTheSentence_ThenTheOpenedKildeCarriesIt()
    {
        Arrive("http://localhost/kilder");
        var cut = Render<KildeExplorer>(b => b.Add(c => c.OpenDataWithoutPage, HostText));

        cut.WaitForAssertion(() => cut.Find(".munin-explorer-kilder tbody th button").Click());

        cut.WaitForAssertion(() => Assert.Contains(Expected, cut.Find(".munin-explorer-kilde__access").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void VariableExplorer_WhenTheHostWordsTheSentence_ThenTheKildeOpenedFromAVariableCarriesIt()
    {
        Arrive("http://localhost/variabler");
        var cut = Render<VariableExplorer>(b => b.Add(c => c.OpenDataWithoutPage, HostText));

        cut.WaitForAssertion(() => cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name")[0].Click());
        cut.WaitForAssertion(() => cut.FindAll(".munin-explorer-detail > button[id]")[0].Click());

        cut.WaitForAssertion(() => Assert.Contains(Expected, cut.Find(".munin-explorer-kilde__access").TextContent, StringComparison.Ordinal));
    }
}
