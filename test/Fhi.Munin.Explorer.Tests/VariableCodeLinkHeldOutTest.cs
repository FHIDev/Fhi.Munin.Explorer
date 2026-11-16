using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Held-out pins for linking a variable by its code (Fhi.Metadata-l9l2n.182), written from the behaviour alone.</summary>
public class VariableCodeLinkHeldOutTest : ExplorerTestContext
{
    private static readonly Guid VariableId = Guid.Parse("7a1f0c4e-2b8d-4e5a-9c3f-1d2e3f4a5b6c");
    private static readonly Guid KildeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid DatasamlingId = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private const string Code = "V_HKR.KJONN";

    private sealed class CodeClient : EmptyMuninExplorerClient, IMuninExplorerClient
    {
        public List<string> CodesAsked { get; } = [];

        public bool AnswersCodes { get; init; } = true;

        public TaskCompletionSource? Gate { get; init; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
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
                TotalCount = 1, PageNumber = 1, Size = pageSize, TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(Detail());

        public async Task<VariableDetail?> GetVariableByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            CodesAsked.Add(code);
            if (Gate is { } gate)
            {
                await gate.Task;
            }

            return AnswersCodes && string.Equals(code, Code, StringComparison.OrdinalIgnoreCase) ? Detail() : null;
        }

        private static VariableDetail Detail() =>
            new()
            {
                Id = VariableId, Code = Code, PreferredTerm = "Kjønn", KildeId = KildeId,
                KildeName = "Hjerte- og karregisteret", KildeShortName = "HKR", DatasamlingId = DatasamlingId,
                DatasamlingName = "Hjerteinfarkt", VersionStatus = "Active",
                AllDatasamlinger = [new DatasamlingReference { Id = DatasamlingId }],
            };
    }

    // The default body of the contract: a client that never overrides the by-code route.
    private sealed class NoRouteClient : EmptyMuninExplorerClient, IMuninExplorerClient
    {
        private readonly CodeClient _rows = new();

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            _rows.SearchVariablesAsync(search, filter, page, pageSize, sort, direction, cancellationToken);

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            _rows.GetVariableAsync(id, includeHistorical, cancellationToken);
    }

    private HistoryRecorder _history = null!;

    private IRenderedComponent<VariableExplorer> Render(IMuninExplorerClient client, string address, string[]? declined = null)
    {
        Services.AddSingleton(client);
        Services.AddSingleton<IJSRuntime>(_history = new HistoryRecorder(JSInterop.JSRuntime));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<VariableExplorer>(b => b.Add(c => c.DeclinedKeys, declined));
    }

    private static void OpenRow(IRenderedComponent<VariableExplorer> cut)
    {
        cut.WaitForElement("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click();
        cut.WaitForElement(".munin-explorer-detail");
    }

    private static string MailLink(IRenderedComponent<VariableExplorer> cut)
    {
        var detail = cut.Find(".munin-explorer-detail");
        var group = detail.QuerySelectorAll("[role=group]").Single(g =>
            detail.QuerySelector($"#{g.GetAttribute("aria-labelledby")}")?.TextContent.Trim() == "Del variabel");
        Assert.Equal(1, group.QuerySelectorAll("button").Count(b => b.TextContent.Trim() == "Kopier lenke"));
        var mail = Uri.UnescapeDataString(group.QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Send via e-post").GetAttribute("href")!);
        return mail[mail.IndexOf("http", StringComparison.Ordinal)..];
    }

    private static void AssertNamedById(string link)
    {
        var query = QueryHelpers.ParseQuery(new Uri(link).Query);
        Assert.Equal(VariableId.ToString(), query["variabelId"].ToString());
        Assert.False(query.ContainsKey("variabel"));
    }

    [Fact]
    public void Arrive_WhenTheReaderLeavesForThePlainPageBeforeTheCodeAnswers_ThenTheLateAnswerOpensNothingAndWritesNoVariable()
    {
        // The answer must not drag a reader who has moved on back into a variable they never asked for.
        var client = new CodeClient { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = Render(client, $"http://localhost/variabler?variabel={Code}");
        cut.WaitForAssertion(() => Assert.Equal([Code], client.CodesAsked));

        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/variabler");
        cut.WaitForElement("ul.munin-explorer-data-list");
        client.Gate.SetResult();

        cut.WaitForElement("ul.munin-explorer-data-list");
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.DoesNotContain(_history.Written, url => url?.Contains("variabel", StringComparison.Ordinal) is true);
    }

    [Fact]
    public void Arrive_WhenTheVariableOpensFromTheCodeAndItsRowIsClosed_ThenThereIsNoBackAndNothingIsPushed()
    {
        // A code arrival is the entry the reader landed on; closing it as Back would leave the site.
        var client = new CodeClient { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = Render(client, $"http://localhost/variabler?variabel={Code}");
        cut.WaitForAssertion(() => Assert.Equal([Code], client.CodesAsked));

        client.Gate.SetResult();
        cut.WaitForElement(".munin-explorer-detail");
        cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".munin-explorer-detail")));
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier is "history.back" or "history.pushState");
    }

    [Fact]
    public void OpenRow_WhenTheClientHasNoCodeRoute_ThenTheShareLinkAndTheAddressNameTheVariableById()
    {
        // A code link opens nothing on a Munin without the route, so the id must travel instead.
        var cut = Render(new NoRouteClient(), "http://localhost/variabler?search=kjonn");

        OpenRow(cut);

        cut.WaitForAssertion(() => AssertNamedById(MailLink(cut)));
        var written = _history.Written.Where(u => u is not null).ToList();
        Assert.NotEmpty(written);
        Assert.Contains($"variabelId={VariableId}", written[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(written, u => u!.Contains("variabel=", StringComparison.Ordinal));
    }

    [Fact]
    public void Arrive_WhenTheHostDeclinesVariabel_ThenAnIdAddressStillOpensItAndTheCodeRouteIsNeverAsked()
    {
        // Declining the code key must not take the older id links down with it.
        var client = new CodeClient();
        var cut = Render(client,
            $"http://localhost/variabler?search={Code}&variabelId={VariableId}&datasamlingId={DatasamlingId}", ["variabel"]);

        cut.WaitForElement(".munin-explorer-detail");
        Assert.Empty(client.CodesAsked);
        Assert.DoesNotContain(_history.Written, u => u?.Contains("variabel=", StringComparison.Ordinal) is true);
    }

    [Fact]
    public void Arrive_WhenTheHostDeclinesVariabelId_ThenACodeAddressAsksNothingAndOpensNothing()
    {
        // With no way to write an open variable, a code in the address is not read either.
        var client = new CodeClient();
        var cut = Render(client, $"http://localhost/variabler?variabel={Code}", ["variabelId"]);

        cut.WaitForElement("ul.munin-explorer-data-list");
        Assert.Empty(client.CodesAsked);
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
    }
}
