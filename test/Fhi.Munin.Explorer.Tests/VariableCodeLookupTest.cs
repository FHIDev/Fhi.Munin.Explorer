using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>How the explorer looks a variable's code up: the wait, a code that fails, and leaving (Fhi.Metadata-l9l2n.188).</summary>
public class VariableCodeLookupTest : ExplorerTestContext
{
    private static readonly Guid KildeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid DatasamlingId = Guid.Parse("66666666-7777-8888-9999-000000000000");

    private static readonly (Guid Id, string Code, string Name) Folded = (Guid.Parse("0a1f0c4e-2b8d-4e5a-9c3f-1d2e3f4a5b6c"), "V_HKR.ALDER", "Alder");
    private static readonly (Guid Id, string Code, string Name) Answered = (Guid.Parse("7a1f0c4e-2b8d-4e5a-9c3f-1d2e3f4a5b6c"), "V_HKR.KJONN", "Kjønn");

    private sealed class LookupClient : EmptyMuninExplorerClient, IMuninExplorerClient
    {
        public List<string> CodesAsked { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public List<Task<VariableDetail?>> Answers { get; } = [];

        /// <summary>Holds every by-code answer; a cancelled token throws, as HttpClient does.</summary>
        public TaskCompletionSource? Gate { get; init; }

        /// <summary>The codes the gate holds; empty, it holds every code.</summary>
        public HashSet<string> Gated { get; init; } = [];

        /// <summary>Codes this Munin answers with null, as a 409 fold or a hidden variable does.</summary>
        public HashSet<string> Unanswered { get; init; } = [];

        public bool Fails { get; init; }

        public bool Throttled { get; init; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [Summary(Folded), Summary(Answered)],
                TotalCount = 2,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(Detail(id == Folded.Id ? Folded : Answered));

        public Task<VariableDetail?> GetVariableByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            CodesAsked.Add(code);
            Tokens.Add(cancellationToken);
            var answer = AnswerAsync(code, cancellationToken);
            Answers.Add(answer);
            return answer;
        }

        private async Task<VariableDetail?> AnswerAsync(string code, CancellationToken cancellationToken)
        {
            if (Gate is { } gate && (Gated.Count == 0 || Gated.Contains(code)))
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            if (Fails)
            {
                throw new HttpRequestException("503");
            }

            if (Throttled)
            {
                throw new MuninExplorerRateLimitedException();
            }

            return Unanswered.Contains(code) ? null
                : code == Folded.Code ? Detail(Folded)
                : code == Answered.Code ? Detail(Answered)
                : null;
        }

        private static VariableSummary Summary((Guid Id, string Code, string Name) v) => new()
        {
            Id = v.Id, Code = v.Code, PreferredTerm = v.Name, KildeId = KildeId,
            KildeName = "Hjerte- og karregisteret", DatasamlingId = DatasamlingId,
            DatasamlingName = "Hjerteinfarkt", VersionStatus = "Active",
        };

        private static VariableDetail Detail((Guid Id, string Code, string Name) v) => new()
        {
            Id = v.Id, Code = v.Code, PreferredTerm = v.Name, KildeId = KildeId,
            KildeName = "Hjerte- og karregisteret", KildeShortName = "HKR", DatasamlingId = DatasamlingId,
            DatasamlingName = "Hjerteinfarkt", VersionStatus = "Active",
            AllDatasamlinger = [new DatasamlingReference { Id = DatasamlingId }],
        };
    }

    private IReadOnlyList<LogEntry> Logged => Services.GetRequiredService<RecordingLoggerProvider>().Entries;

    private IRenderedComponent<VariableExplorer> Render(IMuninExplorerClient client, string address)
    {
        Services.AddSingleton(client);
        Services.AddSingleton<IJSRuntime>(new HistoryRecorder(JSInterop.JSRuntime));
        Services.AddScoped<VariableListState>();
        var recorder = new RecordingLoggerProvider();
        Services.AddSingleton(recorder);
        Services.AddLogging(b => b
            .AddProvider(recorder)
            .SetMinimumLevel(LogLevel.Trace)
            .AddFilter((category, _) => category?.StartsWith("Fhi.Munin.Explorer", StringComparison.Ordinal) == true));
        SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
        return Render<VariableExplorer>();
    }

    private static void Open(IRenderedComponent<VariableExplorer> cut, string name)
    {
        cut.WaitForAssertion(() => cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name")
            .Single(b => b.TextContent.Trim() == name).Click());
        cut.WaitForElement(".munin-explorer-detail");
    }

    private static IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> SharedLink(IRenderedComponent<VariableExplorer> cut)
    {
        var detail = cut.Find(".munin-explorer-detail");
        var mail = Uri.UnescapeDataString(detail.QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Send via e-post").GetAttribute("href")!);
        return QueryHelpers.ParseQuery(new Uri(mail[mail.IndexOf("http", StringComparison.Ordinal)..]).Query);
    }

    private async Task Rerender(IRenderedComponent<VariableExplorer> cut, int times)
    {
        for (var i = 0; i < times; i++)
        {
            cut.Render();
            await cut.InvokeAsync(() => { });
        }
    }

    // Lets the explorer's continuation run after every answer has settled, cancelled ones included.
    private async Task Settle(LookupClient client)
    {
        await Task.WhenAll(client.Answers.Select(a => a.ContinueWith(_ => { }, TaskScheduler.Default)));
        await Renderer.Dispatcher.InvokeAsync(() => { });
    }

    [Fact]
    public void OnInitializedAsync_WhenTheAddressesCodeIsStillLookedUp_ThenAStatusSaysSoUntilTheVariableOpens()
    {
        // Without it the reader sees an empty area while the code is asked.
        var client = new LookupClient { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = Render(client, $"http://localhost/variabler?variabel={Answered.Code}");

        cut.WaitForAssertion(() => Assert.Equal("Henter variabler …", cut.Find("p[role=status]").TextContent.Trim()));
        var status = cut.Find("p[role=status]");
        Assert.Equal(("polite", "true"), (status.GetAttribute("aria-live"), status.GetAttribute("aria-atomic")));

        client.Gate.SetResult();
        cut.WaitForElement(".munin-explorer-detail");
        Assert.DoesNotContain(cut.FindAll("p[role=status]"), p => p.Closest("section.munin-explorer") is null);
    }

    [Fact]
    public async Task Learn_WhenOneVariablesCodeAnswersNothing_ThenAnotherIsLinkedByItsCodeAndTheNullIsNotAskedAgain()
    {
        // A 409 fold or a hidden variable says nothing of the others, and a null is an answer, not a failure to retry.
        var client = new LookupClient { Unanswered = [Folded.Code] };
        var cut = Render(client, "http://localhost/variabler?search=hkr");

        Open(cut, Folded.Name);
        cut.WaitForAssertion(() => Assert.Contains(Folded.Code, client.CodesAsked));
        await Rerender(cut, 5);
        Assert.Single(client.CodesAsked, c => c == Folded.Code);

        Open(cut, Answered.Name);
        cut.WaitForAssertion(() => Assert.Equal(Answered.Code, SharedLink(cut)["variabel"].ToString()));

        Open(cut, Folded.Name);
        cut.WaitForAssertion(() => Assert.Equal(Folded.Id.ToString(), SharedLink(cut)["variabelId"].ToString()));
        Assert.False(SharedLink(cut).ContainsKey("variabel"));
    }

    [Fact]
    public async Task Learn_WhenMuninKeepsFailing_ThenAVariablesCodeIsAskedTwiceAtMost()
    {
        // Every render asks for a code not yet known, so an unbounded retry is two requests and an Error per render.
        var client = new LookupClient { Fails = true };
        var cut = Render(client, "http://localhost/variabler?search=hkr");
        Open(cut, Answered.Name);

        await Rerender(cut, 5);

        Assert.Equal([Answered.Code, Answered.Code], client.CodesAsked.Where(c => c == Answered.Code));
    }

    [Fact]
    public async Task Arrive_WhenTheReaderMovesOnWhileTheCodeIsLookedUp_ThenTheRequestIsCancelledAndNothingIsLogged()
    {
        // The view the code belonged to is gone, so its request has no reader; a cancellation is no failure.
        var client = new LookupClient { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = Render(client, $"http://localhost/variabler?variabel={Answered.Code}");
        cut.WaitForAssertion(() => Assert.Single(client.Tokens));

        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/variabler");

        cut.WaitForAssertion(() => Assert.True(client.Tokens[0].IsCancellationRequested));
        await Settle(client);
        Assert.Empty(Logged);
    }

    [Fact]
    public async Task Dispose_WhileTheAddressesCodeIsLookedUp_ThenTheRequestIsCancelledAndNothingIsLogged()
    {
        // A closed explorer has no reader for the answer.
        var client = new LookupClient { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = Render(client, $"http://localhost/variabler?variabel={Answered.Code}");
        cut.WaitForAssertion(() => Assert.Single(client.Tokens));

        await Renderer.DisposeComponents();

        Assert.True(client.Tokens[0].IsCancellationRequested);
        await Settle(client);
        Assert.Empty(Logged);
    }

    [Fact]
    public async Task Dispose_WhileAnOpenVariablesCodeIsLearned_ThenTheRequestIsCancelledAndNothingIsLogged()
    {
        // Learning a code for the address outlives no explorer, and its cancellation is no failure to retry or log.
        var client = new LookupClient { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = Render(client, "http://localhost/variabler?search=hkr");
        Open(cut, Answered.Name);
        cut.WaitForAssertion(() => Assert.Contains(Answered.Code, client.CodesAsked));

        await Renderer.DisposeComponents();

        Assert.All(client.Tokens, t => Assert.True(t.IsCancellationRequested));
        await Settle(client);
        Assert.Empty(Logged);
    }

    [Fact]
    public async Task Learn_WhenMuninKeepsThrottling_ThenAVariablesCodeIsAskedTwiceAtMost()
    {
        // A 429 has a catch of its own, and an unbounded retry there would keep a throttled Munin throttled.
        var client = new LookupClient { Throttled = true };
        var cut = Render(client, "http://localhost/variabler?search=hkr");
        Open(cut, Answered.Name);

        await Rerender(cut, 5);

        Assert.Equal([Answered.Code, Answered.Code], client.CodesAsked.Where(c => c == Answered.Code));
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_ThenTheSecondCallDoesNothing()
    {
        // A host may dispose twice, and the cancellation sources are gone after the first.
        var cut = Render(new LookupClient(), "http://localhost/variabler?search=hkr");
        cut.WaitForElement("ul.munin-explorer-data-list");

        await cut.Instance.DisposeAsync();
        await cut.Instance.DisposeAsync();
    }

    [Fact]
    public void Learn_WhenAVariableFirstOpenedAfterAnotherResolvedAnswersNothing_ThenItsLinkTurnsToItsId()
    {
        // Its row is drawn with a link by code before the null arrives, and nothing else redraws it.
        var client = new LookupClient
        {
            Unanswered = [Folded.Code], Gated = [Folded.Code], Gate = new(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var cut = Render(client, "http://localhost/variabler?search=hkr");
        Open(cut, Answered.Name);
        cut.WaitForAssertion(() => Assert.Equal(Answered.Code, SharedLink(cut)["variabel"].ToString()));

        Open(cut, Folded.Name);
        cut.WaitForAssertion(() => Assert.Equal(Folded.Code, SharedLink(cut)["variabel"].ToString()));
        client.Gate.SetResult();

        cut.WaitForAssertion(() => Assert.Equal(Folded.Id.ToString(), SharedLink(cut)["variabelId"].ToString()));
        Assert.False(SharedLink(cut).ContainsKey("variabel"));
    }
}
