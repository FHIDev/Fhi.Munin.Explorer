using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

public class VariableWithoutCodeAnswerLinkHeldOutTest : ExplorerTestContext
{
    private static readonly Guid AId = Guid.Parse("a1a1a1a1-2b8d-4e5a-9c3f-1d2e3f4a5b6c");
    private static readonly Guid BId = Guid.Parse("b2b2b2b2-2b8d-4e5a-9c3f-1d2e3f4a5b6c");
    private static readonly Guid KildeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid DatasamlingId = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private const string CodeA = "V_HKR.ALFA";
    private const string CodeB = "V_HKR.BETA";

    private sealed class TwoVariablesClient : EmptyMuninExplorerClient, IMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [Summary(AId, CodeA, "Alfa"), Summary(BId, CodeB, "Beta")],
                TotalCount = 2,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(id == AId ? Detail(AId, CodeA, "Alfa") : Detail(BId, CodeB, "Beta"));

        public Task<VariableDetail?> GetVariableByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(string.Equals(code, CodeB, StringComparison.OrdinalIgnoreCase) ? Detail(BId, CodeB, "Beta") : null);

        private static VariableSummary Summary(Guid id, string code, string term) =>
            new()
            {
                Id = id,
                Code = code,
                PreferredTerm = term,
                KildeId = KildeId,
                KildeName = "Hjerte- og karregisteret",
                DatasamlingId = DatasamlingId,
                DatasamlingName = "Hjerteinfarkt",
                VersionStatus = "Active",
            };

        private static VariableDetail Detail(Guid id, string code, string term) =>
            new()
            {
                Id = id,
                Code = code,
                PreferredTerm = term,
                KildeId = KildeId,
                KildeName = "Hjerte- og karregisteret",
                KildeShortName = "HKR",
                DatasamlingId = DatasamlingId,
                DatasamlingName = "Hjerteinfarkt",
                VersionStatus = "Active",
                AllDatasamlinger = [new DatasamlingReference { Id = DatasamlingId }],
            };
    }

    private static string MailLink(IRenderedComponent<VariableExplorer> cut)
    {
        var detail = cut.Find(".munin-explorer-detail");
        var group = detail.QuerySelectorAll("[role=group]").Single(g =>
            detail.QuerySelector($"#{g.GetAttribute("aria-labelledby")}")?.TextContent.Trim() == "Del variabel");
        var mail = Uri.UnescapeDataString(group.QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Send via e-post").GetAttribute("href")!);
        return mail[mail.IndexOf("http", StringComparison.Ordinal)..];
    }

    private static void OpenRow(IRenderedComponent<VariableExplorer> cut, int index, string term)
    {
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Count));
        cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name")[index].Click();
        cut.WaitForAssertion(() => Assert.Contains(term, cut.Find(".munin-explorer-detail").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void ShareLink_WhenItsOwnCodeAnswersNullAfterAnotherCodeResolved_ThenItNamesTheVariableById()
    {
        // One variable's code resolving must not turn a null-answered variable's link into a code that opens nothing.
        Services.AddSingleton<IMuninExplorerClient>(new TwoVariablesClient());
        Services.AddSingleton<IJSRuntime>(new HistoryRecorder(JSInterop.JSRuntime));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/variabler?search=hkr");
        var cut = Render<VariableExplorer>();

        OpenRow(cut, 0, "Alfa");
        OpenRow(cut, 1, "Beta");
        cut.WaitForAssertion(() => Assert.True(QueryHelpers.ParseQuery(new Uri(MailLink(cut)).Query).ContainsKey("variabel")));
        OpenRow(cut, 0, "Alfa");

        var query = QueryHelpers.ParseQuery(new Uri(MailLink(cut)).Query);
        Assert.False(query.ContainsKey("variabel"));
        Assert.Equal(AId.ToString(), query["variabelId"].ToString());
    }
}
