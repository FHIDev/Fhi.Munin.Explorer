using System.Net;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// That a swallowed exception reaches the host's log, and that a host with no logging at all still
/// gets a working component.
/// </summary>
/// <remarks>
/// Both halves, always. Proving only the first passes CI and leaves a host that registered nothing
/// with a component that throws while rendering — a silent data error turned into a dead page,
/// which is worse than the blindness this replaces (Fhi.Metadata-l9l2n.47).
/// </remarks>
public class ExceptionLoggingTest : BunitContext
{
    /// <summary>The exception a fake throws, recognisable by reference when it is read back.</summary>
    private static readonly HttpRequestException Sentinel = new("SENTINEL-7f3a: the API is down");

    private sealed class FailingKildeClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            throw Sentinel;
    }

    private sealed class FailingSearchClient : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            throw Sentinel;
    }

    private sealed class ThrottledKildeClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
    }

    private sealed class ThrottledCreateClient : EmptyMuninExplorerClient
    {
        public override Task<VariableList> CreateMyListAsync(
            string name, CancellationToken cancellationToken = default) =>
            throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
    }

    private sealed class ThrottledSearchClient : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
    }

    /// <summary>One kilde in the list, and whatever the caller chose out of the detail endpoint.</summary>
    /// <remarks>
    /// Both folded splits in <c>KildeSearch</c> read that one endpoint — the drilldown and the
    /// expanded row's datasamlinger — so the same fake drives each from the click that reaches it.
    /// </remarks>
    private sealed class RefusingKildeDetailClient(Exception refusal) : EmptyMuninExplorerClient
    {
        internal KildeSummary Kilde { get; } = new()
        {
            Id = Guid.NewGuid(),
            Code = "K_ALS",
            Name = "Als registeret",
            Kildetype = "sentraltHelseregister",
            IsActive = true,
            DatasamlingCount = 2,
            TotalVariables = 42,
        };

        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>([Kilde]);

        public override Task<KildeDetail?> GetKildeAsync(
            Guid id, CancellationToken cancellationToken = default) =>
            Task.FromException<KildeDetail?>(refusal);
    }

    /// <summary>A reader whose lists the API will not hand over, for the reason the test chose.</summary>
    private sealed class RefusingListsClient(Exception refusal) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<VariableList>>(refusal);
    }

    /// <summary>Two lists, whose pages the API refuses wherever the test says it does.</summary>
    private sealed class RefusedPagesClient : EmptyMuninExplorerClient
    {
        internal Guid First { get; } = Guid.NewGuid();

        internal Guid Second { get; } = Guid.NewGuid();

        /// <summary>Which page reads are refused, by list and by the size asked for.</summary>
        internal Func<Guid, int, bool> Refuses { get; set; } = (_, _) => false;

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
            [
                new VariableList { Id = First, Name = "Første liste" },
                new VariableList { Id = Second, Name = "Andre liste" },
            ]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Refuses(id, pageSize)
                ? Task.FromException<Page<VariableListItem>?>(
                    new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30)))
                : Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>());
    }

    /// <summary>Every endpoint answering "nothing", so the only thing that can fail is the host.</summary>
    private sealed class QuietClient : EmptyMuninExplorerClient;

    private sealed class UnauthorizedCreateClient : EmptyMuninExplorerClient
    {
        public override Task<VariableList> CreateMyListAsync(
            string name, CancellationToken cancellationToken = default) =>
            throw new MuninExplorerUnauthorizedException();
    }

    /// <summary>A hierarchy whose every call is left open for the test to fail when it chooses.</summary>
    /// <remarks>
    /// Deliberately without <c>RunContinuationsAsynchronously</c>: the component's catch then runs
    /// inside the call to <see cref="Fail"/>, so an assertion on the next line cannot read the log
    /// before the line that would have been written was reached.
    /// </remarks>
    private sealed class OpenHierarchyClient : EmptyMuninExplorerClient
    {
        private readonly List<TaskCompletionSource<KildeHierarchy?>> _calls = [];

        internal void Fail(int call, Exception cause) => _calls[call].TrySetException(cause);

        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(
            Guid id, CancellationToken cancellationToken = default)
        {
            var call = new TaskCompletionSource<KildeHierarchy?>();

            _calls.Add(call);

            return call.Task;
        }
    }

    [Fact]
    public void KildeSearch_WhenTheKildeListCallThrows_ThenTheExceptionItselfReachesTheHostsLogger()
    {
        var recorder = Recording();

        var cut = RenderWith<KildeSearch>(new FailingKildeClient());

        var entry = Assert.Single(recorder.Entries, e => e.Level == LogLevel.Error);

        // The exception instance, not "a log call happened". A component that wrote the sentence
        // on screen and dropped the stack would satisfy the weaker assertion, and that is the
        // defect this whole change is about.
        Assert.Same(Sentinel, entry.Exception);
        Assert.Equal(typeof(KildeSearch).FullName, entry.Category);

        // Unchanged, and asserted beside the log: this adds diagnostics, it does not move what the
        // reader is told or when.
        Assert.Contains(Texts.For("no").KildeListError, cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void KildeSearch_WhenTheApiRateLimits_ThenItIsAWarningAndStaysItsOwnBranch()
    {
        var recorder = Recording();

        var cut = RenderWith<KildeSearch>(new ThrottledKildeClient());

        // Warning rather than Error, because the catalogue is up. Telling the two apart from
        // outside is what ruled rate limiting out of the incident this bead came from.
        var entry = Assert.Single(recorder.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerRateLimitedException>(entry.Exception);
        Assert.Contains(Texts.For("no").RateLimitError, cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public async Task VariableListView_WhenCreatingAListIsThrottled_ThenItIsAWarningTooOnAWritePath()
    {
        // The split is asserted on a second component and on a write rather than a read, because
        // fifteen typed handlers carry it and the guard can only say that each logs, not that a
        // reader of the Error channel is being told the truth about which of them fired.
        var recorder = Recording();

        Services.AddSingleton<IMuninExplorerClient>(new ThrottledCreateClient());
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));

        cut.Find("button[id^='munin-explorer-create-toggle-']").Click();
        cut.Find("input[id^='munin-explorer-new-list-']").Change("Ny liste");
        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Trim() == Texts.For("no").CreateList).Click());

        var entry = Assert.Single(recorder.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerRateLimitedException>(entry.Exception);

        // The name the reader typed stays out of it, which is the other half of the rule.
        Assert.DoesNotContain("Ny liste", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KildeHierarchyView_WhenTheReaderMovesOnAndTheOldFetchIsCancelled_ThenNothingIsLogged()
    {
        // This is the one component that cancels its own calls, so the ordinary act of clicking a
        // second kilde used to write an Error naming a kilde that was never in trouble — the exact
        // noise that makes the channel unreadable at the moment somebody needs to read it.
        var recorder = Recording();
        var client = new OpenHierarchyClient();

        Services.AddSingleton<IMuninExplorerClient>(client);

        var cut = Render<KildeHierarchyView>(p => p.Add(c => c.KildeId, Guid.NewGuid()));

        // The second kilde cancels the first call, which then comes back the way HttpClient reports
        // a cancellation — as a TaskCanceledException, and not as anything the type can be read off.
        cut.Render(p => p.Add(c => c.KildeId, Guid.NewGuid()));
        await cut.InvokeAsync(() => client.Fail(0, new TaskCanceledException()));

        Assert.Empty(recorder.Entries);

        // The negative control, and the reason a blanket OperationCanceledException filter would be
        // wrong: the very same type on the call still on screen is HttpClient's own 30-second
        // timeout, which is a fault and is logged.
        await cut.InvokeAsync(() => client.Fail(1, new TaskCanceledException()));

        Assert.Equal(LogLevel.Error, Assert.Single(recorder.Entries).Level);
    }

    [Fact]
    public void KildeSearch_WhenTheHostsOwnLoggerThrows_ThenTheReaderStillGetsTheSentence()
    {
        // The seam is the whole premise of shipping this into someone else's CMS: Logger<T> rethrows
        // a provider's failure as an AggregateException, and the log call is the first statement of
        // a catch written so that nothing escapes and takes the circuit with it.
        Services.AddLogging(b => b.AddProvider(new ThrowingLoggerProvider()));

        var cut = RenderWith<KildeSearch>(new FailingKildeClient());

        Assert.Contains(Texts.For("no").KildeListError, cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void VariableSearch_WhenTheSearchThrows_ThenTheExceptionItselfReachesTheHostsLogger()
    {
        // A second component, so the mechanism is shown to be the package's rather than one
        // component's: twenty-four sites were blind, and the next incident lands on another line.
        var recorder = Recording();

        var cut = RenderWith<VariableSearch>(new FailingSearchClient());

        var entry = Assert.Single(recorder.Entries, e => e.Level == LogLevel.Error);

        Assert.Same(Sentinel, entry.Exception);
        Assert.Equal(typeof(VariableSearch).FullName, entry.Category);
        Assert.Contains(Texts.For("no").Error, cut.Markup);
    }

    [Fact]
    public async Task KildeSearch_WhenTheHostRegisteredNoLoggingAtAll_ThenItStillDrawsTheErrorText()
    {
        // The trap. A non-nullable [Inject] ILogger<T> throws at render when nothing is registered,
        // which would turn a swallowed data error into a dead component. bUnit cannot model this —
        // its own renderer needs an ILoggerFactory — so the component is rendered directly.
        var html = await RenderedWithoutLogging<KildeSearch>(new FailingKildeClient());

        Assert.Contains(Texts.For("no").KildeListError, html);
    }

    [Fact]
    public async Task VariableSearch_WhenTheHostRegisteredNoLoggingAtAll_ThenItStillDrawsTheErrorText()
    {
        var html = await RenderedWithoutLogging<VariableSearch>(new FailingSearchClient());

        Assert.Contains(Texts.For("no").Error, html);
    }

    [Fact]
    public void AddMuninExplorer_WhenTheHostConfiguredNoLogging_ThenTheComponentsHaveOneAnyway()
    {
        // Where the guarantee comes from: every host calls this to get the client at all, so the
        // gap is closed once at the registration site rather than at each catch.
        var services = new ServiceCollection();

        services.AddMuninExplorer(o => o.ApiBaseUrl = "https://runa.munin.skytest.fhi.no");

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ILogger<KildeSearch>>());
    }

    [Fact]
    public void AddMuninExplorer_WhenTheHostConfiguredItsOwnLogging_ThenNothingItSetIsReplaced()
    {
        // AddLogging is TryAdd-based inside, but "idempotent" is the kind of claim that is worth a
        // test: a host that lost its providers here would lose them for its whole application.
        var recorder = new RecordingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddProvider(recorder).SetMinimumLevel(LogLevel.Trace));
        services.AddMuninExplorer(o => o.ApiBaseUrl = "https://runa.munin.skytest.fhi.no");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ILogger<KildeSearch>>().LogError("host provider still installed");

        Assert.Single(recorder.Entries);
    }

    [Fact]
    public void VariableSearch_WhenTheHostsOwnCallbackThrows_ThenTheExceptionReachesTheHostsLogger()
    {
        // The one path where the logger is threaded by hand rather than read off the component:
        // RaiseAsync is static and takes it as an argument at fourteen call sites, so a site that
        // passed null would be a hole no guard over catch clauses could see.
        var recorder = Recording();

        Services.AddSingleton<IMuninExplorerClient>(new QuietClient());

        // A host rewriting a CMS URL from this handler is the case the remarks call most likely to
        // break, and it fires on the initial render as well as on every later search.
        var cut = Render<VariableSearch>(p => p.Add<string?>(c => c.SearchChanged, _ => throw Sentinel));

        var entry = Assert.Single(recorder.Entries, e => e.Level == LogLevel.Error);

        Assert.Same(Sentinel, entry.Exception);
        Assert.Equal(typeof(VariableSearch).FullName, entry.Category);

        // And the host's broken handler is the host's to find: the component says nothing about it
        // on top of what the search itself reported.
        Assert.DoesNotContain(Texts.For("no").Error, cut.Markup);
    }

    [Fact]
    public void VariableSearch_WhenTheResultPageIsThrottled_ThenItIsAWarningRatherThanAFault()
    {
        // The first of the three folded splits, where Warning-versus-Error is hand-written logic in
        // a catch (Exception) body rather than a clause the guard can read. Inverting the condition
        // or dropping the branch leaves the guard green, so each of the three has a test of its own.
        var recorder = Recording();

        var cut = RenderWith<VariableSearch>(new ThrottledSearchClient());

        var entry = Assert.Single(recorder.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerRateLimitedException>(entry.Exception);
        Assert.Contains(Texts.For("no").RateLimitError, cut.Markup);
    }

    [Fact]
    public void KildeSearch_WhenTheDrilldownIsThrottled_ThenItIsAWarningRatherThanAFault()
    {
        // The second: the kilde the reader opened. The list arrives and the detail is refused,
        // which is what opening one kilde after another meets while the catalogue is perfectly up.
        var recorder = Recording();
        var client = new RefusingKildeDetailClient(
            new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30)));

        var cut = RenderWith<KildeSearch>(client);

        cut.Find(".munin-explorer-kilder tbody th button").Click();

        var entry = Assert.Single(recorder.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerRateLimitedException>(entry.Exception);
        Assert.Contains(client.Kilde.Id.ToString(), entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KildeSearch_WhenTheDatasamlingerAreThrottled_ThenItIsAWarningRatherThanAFault()
    {
        // The third, and the one the reader reaches most often: expanding a row sends a request per
        // press, which is exactly the rhythm the per-address limiter counts.
        var recorder = Recording();
        var client = new RefusingKildeDetailClient(
            new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30)));

        var cut = RenderWith<KildeSearch>(client);

        cut.Find(".munin-explorer-kilder__expand-toggle").Click();

        var entry = Assert.Single(recorder.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerRateLimitedException>(entry.Exception);
    }

    [Fact]
    public void VariableListView_WhenTheListsReadOnMountIsThrottled_ThenItIsAWarning()
    {
        // The my-lists paths reach the same limiter as everything else, and the mount fires this
        // read alongside the search and the facet refresh — the burst the limiter is counting.
        var entry = MountedWith(new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30)));

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerRateLimitedException>(entry.Exception);
    }

    [Fact]
    public void VariableListView_WhenTheApiRefusesTheReaderOnMount_ThenItIsAWarningToo()
    {
        // The 401 half. A host can declare IsAuthenticated true while its token provider sends
        // nothing the API accepts, which fills the Error channel with an expected, handled outcome.
        var entry = MountedWith(new MuninExplorerUnauthorizedException());

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerUnauthorizedException>(entry.Exception);
    }

    [Fact]
    public void VariableListView_WhenTheListsReadOnMountFails_ThenItIsStillAnError()
    {
        // The other half of the same branch, so a split that always said Warning would fail here:
        // the API being down is a fault, and the level is the only thing telling the two apart.
        var entry = MountedWith(Sentinel);

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(Sentinel, entry.Exception);
    }

    [Fact]
    public void VariableListView_WhenAPageOfTheListIsThrottled_ThenItIsAWarning()
    {
        // Refused by the size asked for, which is what separates this site from the mount's: the
        // membership walk asks for the API's ceiling of 1000 and the view's own page read for 25.
        var recorder = Recording();

        Services.AddSingleton<IMuninExplorerClient>(
            new RefusedPagesClient { Refuses = (_, size) => size == 25 });
        Services.AddScoped<VariableListState>();

        Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));

        var entry = Assert.Single(recorder.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerRateLimitedException>(entry.Exception);
    }

    [Fact]
    public async Task VariableListView_WhenTheSwitchToAnotherListIsThrottled_ThenItIsAWarning()
    {
        // The picker, which is a press per list the reader looks at — and the last of the folded
        // sites on these paths. The mount is let through so the failure is the switch and nothing
        // around it.
        var recorder = Recording();
        var client = new RefusedPagesClient();

        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));

        client.Refuses = (id, _) => id == client.Second;

        await cut.InvokeAsync(() => cut.Find("select").Change(client.Second.ToString()));

        var entry = Assert.Single(recorder.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerRateLimitedException>(entry.Exception);
    }

    [Fact]
    public async Task VariableSearch_WhenTheMembershipReadIsRefused_ThenItIsAWarning()
    {
        // This one runs on every parameter set — every search and every page turn — so an Error
        // here is the loudest line in a host's channel for a reader who is merely signed out.
        var recorder = Recording();

        Services.AddSingleton<IMuninExplorerClient>(
            new RefusingListsClient(new MuninExplorerUnauthorizedException()));
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, true));

        // Rendered again, because "every parameter set" is the part that makes the level matter.
        await cut.InvokeAsync(() => cut.Render(p => p.Add(c => c.IsAuthenticated, true)));

        Assert.All(recorder.Entries, e => Assert.Equal(LogLevel.Warning, e.Level));
        Assert.NotEmpty(recorder.Entries);
        Assert.All(recorder.Entries, e => Assert.IsType<MuninExplorerUnauthorizedException>(e.Exception));
    }

    [Fact]
    public void VariableListFilters_WhenTheMembershipReadIsRefused_ThenItIsAWarning()
    {
        // The third surface asking the same question on mount, and the third copy of the split.
        var recorder = Recording();

        Services.AddSingleton<IMuninExplorerClient>(
            new RefusingListsClient(new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30))));
        Services.AddScoped<VariableListState>();

        Render<VariableListFilters>(p => p.Add(c => c.IsAuthenticated, true));

        var entry = Assert.Single(recorder.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(typeof(VariableListFilters).FullName, entry.Category);
    }

    [Fact]
    public async Task VariableListView_WhenCreatingAListIsRefusedAsUnauthorised_ThenItIsAWarning()
    {
        // The write half of the 401, told apart from a fault the way the save button already tells
        // it apart. What the reader is shown does not move: there is nothing more to say than that
        // the list was not made.
        var recorder = Recording();

        Services.AddSingleton<IMuninExplorerClient>(new UnauthorizedCreateClient());
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));

        cut.Find("button[id^='munin-explorer-create-toggle-']").Click();
        cut.Find("input[id^='munin-explorer-new-list-']").Change("Ny liste");
        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Trim() == Texts.For("no").CreateList).Click());

        var entry = Assert.Single(recorder.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<MuninExplorerUnauthorizedException>(entry.Exception);
        Assert.DoesNotContain("Ny liste", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>The one entry a saved-list view writes when its lists cannot be read on mount.</summary>
    private LogEntry MountedWith(Exception refusal)
    {
        var recorder = Recording();

        Services.AddSingleton<IMuninExplorerClient>(new RefusingListsClient(refusal));
        Services.AddScoped<VariableListState>();

        Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));

        return Assert.Single(recorder.Entries);
    }

    private RecordingLoggerProvider Recording()
    {
        var recorder = new RecordingLoggerProvider();

        // Filtered to this package's categories: bUnit's own renderer logs at Debug through the
        // same factory, and eight framework lines around one of ours is not a collection any
        // assertion here can read.
        Services.AddLogging(b => b
            .AddProvider(recorder)
            .SetMinimumLevel(LogLevel.Trace)
            .AddFilter((category, _) =>
                category?.StartsWith("Fhi.Munin.Explorer", StringComparison.Ordinal) == true));

        return recorder;
    }

    private IRenderedComponent<TComponent> RenderWith<TComponent>(IMuninExplorerClient client)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        Services.AddSingleton(client);

        return Render<TComponent>();
    }

    /// <summary>
    /// The component rendered against a container holding the client and nothing else.
    /// </summary>
    /// <remarks>
    /// The renderer is handed a logger factory of its own so that the container need not have one:
    /// what is being modelled is the host's services, not the framework's plumbing.
    /// </remarks>
    private static async Task<string> RenderedWithoutLogging<TComponent>(IMuninExplorerClient client)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        var services = new ServiceCollection();
        services.AddSingleton(client);

        await using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<ILoggerFactory>());

        // The seam the logger is resolved through, asserted rather than assumed: [Inject] on a
        // non-nullable IServiceProvider would be a host contract if a container could fail to have
        // one, and no container can — which is why three components already carried it.
        Assert.NotNull(provider.GetService<IServiceProvider>());

        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<TComponent>()).ToHtmlString());

        // Decoded, because a static render escapes the å and the ø the sentences are full of.
        return WebUtility.HtmlDecode(html);
    }
}

/// <summary>A host whose logging is broken — a full disk, a misconfigured provider.</summary>
internal sealed class ThrowingLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Thrower();

    public void Dispose()
    {
    }

    private sealed class Thrower : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            throw new InvalidOperationException("the sink is broken");

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("the sink is broken");
    }
}

/// <summary>What a logger was told, kept so a test can read the exception back off it.</summary>
internal sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

/// <summary>An <see cref="ILoggerProvider"/> that remembers everything written through it.</summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly List<LogEntry> _entries = [];

    internal IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Recorder(this, categoryName);

    public void Dispose()
    {
    }

    private void Record(LogEntry entry)
    {
        lock (_entries)
        {
            _entries.Add(entry);
        }
    }

    private sealed class Recorder(RecordingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            owner.Record(new LogEntry(logLevel, category, formatter(state, exception), exception));
    }
}
