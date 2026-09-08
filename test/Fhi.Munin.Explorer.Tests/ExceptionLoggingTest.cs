using System.Net;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
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

        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<TComponent>()).ToHtmlString());

        // Decoded, because a static render escapes the å and the ø the sentences are full of.
        return WebUtility.HtmlDecode(html);
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
