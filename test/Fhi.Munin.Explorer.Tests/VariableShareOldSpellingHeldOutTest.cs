using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

public class VariableShareOldSpellingHeldOutTest : ExplorerTestContext
{
    private static readonly Guid VariableId = Guid.Parse("7a1f0c4e-2b8d-4e5a-9c3f-1d2e3f4a5b6c");
    private static readonly Guid KildeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid DatasamlingId = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private const string Code = "V_HKR.KJONN";

    private sealed class CodedClient : EmptyMuninExplorerClient, IMuninExplorerClient
    {
        public List<string?> Searches { get; } = [];

        public List<string> CodesAsked { get; } = [];

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(search);
            return Task.FromResult(new Page<VariableSummary>
            {
                Items =
                [
                    new VariableSummary
                    {
                        Id = VariableId, Code = Code, PreferredTerm = "Kjønn", KildeId = KildeId,
                        KildeName = "Hjerte- og karregisteret", DatasamlingId = DatasamlingId,
                        DatasamlingName = "Hjerteinfarkt", VersionStatus = "Active",
                    },
                ],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });
        }

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(Detail());

        public Task<VariableDetail?> GetVariableByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            CodesAsked.Add(code);
            return Task.FromResult<VariableDetail?>(string.Equals(code, Code, StringComparison.OrdinalIgnoreCase) ? Detail() : null);
        }

        private static VariableDetail Detail() =>
            new()
            {
                Id = VariableId,
                Code = Code,
                PreferredTerm = "Kjønn",
                KildeId = KildeId,
                KildeName = "Hjerte- og karregisteret",
                KildeShortName = "HKR",
                DatasamlingId = DatasamlingId,
                DatasamlingName = "Hjerteinfarkt",
                VersionStatus = "Active",
                AllDatasamlinger = [new DatasamlingReference { Id = DatasamlingId, Code = "K_HJERTEINFARKT" }],
            };
    }

    [Fact]
    public void Navigate_WhenTheHostNavigatesToTheOpenPrimaryRowsOlderSpelling_ThenNothingRemounts()
    {
        // The row opened by a click is written with its datasamling code; the older spelling is the same view.
        var client = new CodedClient();
        var history = new HistoryRecorder(JSInterop.JSRuntime);
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddSingleton<IJSRuntime>(history);
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/variabler?search=kjonn");
        var cut = Render<VariableExplorer>();
        cut.WaitForElement("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click();
        cut.WaitForElement(".munin-explorer-detail");
        cut.WaitForAssertion(() => Assert.Contains("datasamling=K_HJERTEINFARKT", history.Written.LastOrDefault() ?? ""));
        var (searches, codes) = (client.Searches.Count, client.CodesAsked.Count);

        navigation.NavigateTo($"http://localhost/variabler?search=kjonn&variabel={Code}");

        Assert.Equal((searches, codes), (client.Searches.Count, client.CodesAsked.Count));
        Assert.NotNull(cut.Find(".munin-explorer-detail"));
    }
}
