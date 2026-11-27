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

        /// <summary>Throws as HttpClient's own timeout does: an OperationCanceledException on a token nobody cancelled.</summary>
        public bool TimesOut { get; init; }

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

        public List<(Guid Id, CancellationToken Token)> DetailsAsked { get; } = [];

        /// <summary>Codes this Munin answers with another variable's detail.</summary>
        public Dictionary<string, (Guid Id, string Code, string Name)> AnswersWith { get; init; } = [];

        /// <summary>Codes answered with null the first time only.</summary>
        public HashSet<string> UnansweredOnce { get; init; } = [];

        /// <summary>Per code, an answer held until set, whatever the token says, as a request already sent may finish.</summary>
        public Dictionary<string, TaskCompletionSource> Holds { get; init; } = [];

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            DetailsAsked.Add((id, cancellationToken));
            return Task.FromResult<VariableDetail?>(Detail(id == Folded.Id ? Folded : Answered));
        }

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
            if (Holds.TryGetValue(code, out var hold))
            {
                await hold.Task;
            }

            if (Gate is { } gate && (Gated.Count == 0 || Gated.Contains(code)))
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            if (UnansweredOnce.Remove(code))
            {
                return null;
            }

            if (AnswersWith.TryGetValue(code, out var other))
            {
                return Detail(other);
            }

            if (Fails)
            {
                throw new HttpRequestException("503");
            }

            if (Throttled)
            {
                throw new MuninExplorerRateLimitedException();
            }

            if (TimesOut)
            {
                throw new TaskCanceledException("timeout");
            }

            return Unanswered.Contains(code) ? null
                : code == Folded.Code ? Detail(Folded)
                : code == Answered.Code ? Detail(Answered)
                : null;
        }

        private static VariableSummary Summary((Guid Id, string Code, string Name) v) => new()
        {
            Id = v.Id,
            Code = v.Code,
            PreferredTerm = v.Name,
            KildeId = KildeId,
            KildeName = "Hjerte- og karregisteret",
            DatasamlingId = DatasamlingId,
            DatasamlingName = "Hjerteinfarkt",
            VersionStatus = "Active",
        };

        private static VariableDetail Detail((Guid Id, string Code, string Name) v) => new()
        {
            Id = v.Id,
            Code = v.Code,
            PreferredTerm = v.Name,
            KildeId = KildeId,
            KildeName = "Hjerte- og karregisteret",
            KildeShortName = "HKR",
            DatasamlingId = DatasamlingId,
            DatasamlingName = "Hjerteinfarkt",
            VersionStatus = "Active",
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
    public void OnInitializedAsync_WhenTheAddressCodeIsStillLookedUp_ThenAStatusSaysSoUntilTheVariableOpens()
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
    public async Task DisposeAsync_WhenTheAddressCodeIsStillLookedUp_ThenTheRequestIsCancelledAndNothingIsLogged()
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
    public async Task DisposeAsync_WhenAnOpenVariablesCodeIsStillBeingLearned_ThenTheRequestIsCancelledAndNothingIsLogged()
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
            Unanswered = [Folded.Code],
            Gated = [Folded.Code],
            Gate = new(TaskCreationOptions.RunContinuationsAsynchronously),
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

    [Fact]
    public async Task Learn_WhenTheClientTimesOut_ThenItIsLoggedAndAskedOnceMore()
    {
        // A timeout is an OperationCanceledException too, and only our own cancellation may go unlogged.
        var client = new LookupClient { TimesOut = true };
        var cut = Render(client, "http://localhost/variabler?search=hkr");
        Open(cut, Answered.Name);

        await Rerender(cut, 5);

        Assert.Equal([Answered.Code, Answered.Code], client.CodesAsked.Where(c => c == Answered.Code));
        Assert.Equal(2, Logged.Count(e => e.Level == LogLevel.Error && e.Exception is TaskCanceledException));
    }

    [Fact]
    public void OnInitializedAsync_WhenTheAddressCodeTimesOut_ThenItIsLogged()
    {
        var client = new LookupClient { TimesOut = true };
        var cut = Render(client, $"http://localhost/variabler?variabel={Answered.Code}");

        cut.WaitForElement("ul.munin-explorer-data-list");

        Assert.Contains(Logged, e => e.Level == LogLevel.Error && e.Exception is TaskCanceledException);
    }

    [Theory]
    [InlineData("search=hkr")]
    [InlineData("variabelId=7a1f0c4e-2b8d-4e5a-9c3f-1d2e3f4a5b6c")]
    public async Task Learn_WhenMuninHasNoCodeRoute_ThenAnOpenedVariableCostsOneDetailAndOneCodeRequest(string arrival)
    {
        // The contract's remarks and the changelog promise a host this count: the detail for its code, then the code, once.
        var client = new LookupClient { Unanswered = [Folded.Code, Answered.Code] };
        var cut = Render(client, $"http://localhost/variabler?{arrival}");
        if (arrival.StartsWith("search", StringComparison.Ordinal))
        {
            Open(cut, Answered.Name);
        }

        cut.WaitForAssertion(() => Assert.Contains(Answered.Code, client.CodesAsked));
        await Rerender(cut, 5);

        Assert.Single(client.CodesAsked, c => c == Answered.Code);
        Assert.Single(client.DetailsAsked, d => d.Id == Answered.Id && d.Token.CanBeCanceled);
    }

    [Fact]
    public async Task DisposeAsync_WhenAnOpenVariablesDetailIsFetchedForItsCode_ThenThatRequestIsCancelled()
    {
        // The detail fetch is the first of Learn's two requests, and it outlives no explorer either.
        var client = new LookupClient();
        var cut = Render(client, $"http://localhost/variabler?variabelId={Answered.Id}");
        cut.WaitForAssertion(() => Assert.Contains(client.DetailsAsked, d => d.Id == Answered.Id && d.Token.CanBeCanceled));
        var token = client.DetailsAsked.First(d => d.Id == Answered.Id && d.Token.CanBeCanceled).Token;

        await Renderer.DisposeComponents();

        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void Learn_WhenAVariablesCodeAnswersAnotherVariable_ThenItIsLinkedByItsId()
    {
        // A code Munin reads as another variable would open that one from the shared link.
        var client = new LookupClient { AnswersWith = { [Folded.Code] = Answered } };
        var cut = Render(client, "http://localhost/variabler?search=hkr");
        Open(cut, Answered.Name);
        cut.WaitForAssertion(() => Assert.Equal(Answered.Code, SharedLink(cut)["variabel"].ToString()));

        Open(cut, Folded.Name);

        cut.WaitForAssertion(() => Assert.Equal(Folded.Id.ToString(), SharedLink(cut)["variabelId"].ToString()));
        Assert.False(SharedLink(cut).ContainsKey("variabel"));
    }

    [Fact]
    public void OnInitializedAsync_WhenANorwegianReaderWaitsForTheCodeLookup_ThenTheStatusIsMarkedNorwegian()
    {
        // WCAG 3.1.2 both ways: the status sits outside VariableSearch's lang, so it carries its own.
        var client = new LookupClient { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cut = Render(client, $"http://localhost/variabler?variabel={Answered.Code}");

        var status = cut.WaitForElement("p[role=status]");

        Assert.Equal("no", status.GetAttribute("lang"));
    }

    [Fact]
    public async Task Arrive_WhenALeftLookupAnswersAfterTheNextOneStarted_ThenLeavingStillCancelsTheNextOne()
    {
        // A request already sent can finish after its cancellation; its cleanup must not drop the lookup that replaced it.
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new LookupClient
        {
            Holds = { [Folded.Code] = held, [Answered.Code] = new(TaskCreationOptions.RunContinuationsAsynchronously) },
        };
        var cut = Render(client, $"http://localhost/variabler?variabel={Folded.Code}");
        cut.WaitForAssertion(() => Assert.Single(client.Tokens));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"http://localhost/variabler?variabel={Answered.Code}");
        cut.WaitForAssertion(() => Assert.Equal(2, client.Tokens.Count));

        held.SetResult();
        await client.Answers[0];
        await Renderer.Dispatcher.InvokeAsync(() => { });
        navigation.NavigateTo("http://localhost/variabler");

        cut.WaitForAssertion(() => Assert.True(client.Tokens[1].IsCancellationRequested));
    }

    [Fact]
    public void ResolveAsync_WhenAVariableLinkedByIdLaterArrivesByItsCode_ThenItIsLinkedByItsCode()
    {
        // The code that once answered nothing has now answered this very variable, so its link may name it.
        var client = new LookupClient { UnansweredOnce = [Folded.Code] };
        var cut = Render(client, "http://localhost/variabler?search=hkr");
        Open(cut, Answered.Name);
        cut.WaitForAssertion(() => Assert.Equal(Answered.Code, SharedLink(cut)["variabel"].ToString()));
        Open(cut, Folded.Name);
        cut.WaitForAssertion(() => Assert.Equal(Folded.Id.ToString(), SharedLink(cut)["variabelId"].ToString()));

        Services.GetRequiredService<NavigationManager>().NavigateTo($"http://localhost/variabler?variabel={Folded.Code}");

        cut.WaitForAssertion(() => Assert.Equal(Folded.Code, SharedLink(cut)["variabel"].ToString()));
    }
}
