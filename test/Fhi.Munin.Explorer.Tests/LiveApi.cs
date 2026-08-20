using System.Net;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Where the contract-drift tests point, and whether they are allowed to leave the machine.
/// </summary>
/// <remarks>
/// Off unless <see cref="EnabledVariable"/> says otherwise. A test that reaches the network on
/// every <c>dotnet test</c> makes the whole suite as reliable as somebody else's server and as
/// fast as their slowest endpoint, which is how a suite stops being run at all. The scheduled
/// workflow sets the variable; nothing else does.
/// </remarks>
internal static class LiveApi
{
    /// <summary>Set to anything but <c>0</c> or <c>false</c> to let the drift tests call the API.</summary>
    public const string EnabledVariable = "MUNIN_EXPLORER_LIVE";

    /// <summary>
    /// Which API to check, spelled the way a host spells it in configuration
    /// (<c>MuninExplorer:ApiBaseUrl</c>), so there is one name to remember rather than two.
    /// </summary>
    public const string BaseUrlVariable = "MuninExplorer__ApiBaseUrl";

    /// <summary>The public, anonymous test API the sample hosts already read. No secret to hold.</summary>
    public const string DefaultBaseUrl = "https://munin.skytest.fhi.no";

    /// <summary>The name <c>IHttpClientFactory</c> gives the explorer's client.</summary>
    /// <remarks>
    /// <c>AddHttpClient&lt;IMuninExplorerClient, …&gt;</c> names the client after the interface, and
    /// that name is how a handler is attached to the pipeline the real client uses. If the
    /// convention ever changes, nothing is recorded and
    /// <see cref="LiveApiConnection.RoundTripAsync{T}"/> says so rather than passing.
    /// </remarks>
    public const string ClientName = nameof(IMuninExplorerClient);

    public static bool IsEnabled =>
        Environment.GetEnvironmentVariable(EnabledVariable) is { Length: > 0 } enabled
        && !enabled.Equals("0", StringComparison.OrdinalIgnoreCase)
        && !enabled.Equals("false", StringComparison.OrdinalIgnoreCase);

    public static string BaseUrl =>
        Environment.GetEnvironmentVariable(BaseUrlVariable) is { Length: > 0 } url ? url : DefaultBaseUrl;
}

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself unless <see cref="LiveApi.EnabledVariable"/> is set.
/// </summary>
/// <remarks>
/// Skipped rather than filtered out, so the reason is written where somebody reading the test
/// output can see it — a filter that excluded them would leave a run that looks like it covered
/// everything. Internal like every other helper here: xUnit discovers and honours the attribute
/// from inside the test assembly, so there is nothing for it to be public for.
/// </remarks>
internal sealed class LiveApiFactAttribute : FactAttribute
{
    public LiveApiFactAttribute()
    {
        if (!LiveApi.IsEnabled)
        {
            Skip = $"Calls the live API. Set {LiveApi.EnabledVariable}=1 to run it " +
                   $"(against {LiveApi.BaseUrl}; override with {LiveApi.BaseUrlVariable}).";
        }
    }
}

/// <summary>One recorded exchange with the API — the body exactly as it arrived.</summary>
internal sealed record RecordedResponse(Uri? Uri, HttpStatusCode Status, string Body);

/// <summary>What the recording handler writes to, and the tests read from.</summary>
/// <remarks>
/// Separate from the handler because <c>IHttpClientFactory</c> owns the handlers it builds — it
/// creates them per pipeline and disposes them when the pipeline expires — so a handler is the
/// wrong place to keep anything a test still needs afterwards.
/// </remarks>
internal sealed class ResponseLog
{
    private readonly List<RecordedResponse> responses = [];

    public IReadOnlyList<RecordedResponse> Responses => responses;

    public void Add(RecordedResponse response) => responses.Add(response);

    public void Clear() => responses.Clear();
}

/// <summary>Copies each response body out on its way through, leaving the response usable.</summary>
internal sealed class RecordingHandler(ResponseLog log) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // ReadAsStringAsync buffers the content before it reads it, so the client can still read
        // the same response afterwards. Recording by consuming the network stream would leave the
        // client with nothing, which is a test that breaks the thing it is measuring.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        log.Add(new RecordedResponse(request.RequestUri, response.StatusCode, body));

        return response;
    }
}

/// <summary>
/// The real client, wired the way a host wires it, with every response body kept for comparison.
/// </summary>
/// <remarks>
/// Deliberately the real <see cref="IMuninExplorerClient"/> rather than a hand-written request:
/// the URLs, the query strings and the deserialiser options are all part of what can drift, and a
/// test that spells them out itself would keep passing after the client stopped working.
/// </remarks>
internal sealed class LiveApiConnection : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly ResponseLog log;

    private LiveApiConnection(ServiceProvider provider, ResponseLog log)
    {
        this.provider = provider;
        this.log = log;
        Client = provider.GetRequiredService<IMuninExplorerClient>();
    }

    public IMuninExplorerClient Client { get; }

    /// <summary>Opens a connection to <see cref="LiveApi.BaseUrl"/>.</summary>
    /// <param name="answeredBy">
    /// Answers the requests instead of the network. Only <see cref="ShapeDriftTest"/> passes one:
    /// it is what lets the nightly job's whole path — client, recording, comparison, failure — be
    /// proved against a payload we have deliberately broken, on a runner with no network at all.
    /// </param>
    public static LiveApiConnection Open(HttpMessageHandler? answeredBy = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ResponseLog>();
        services.AddTransient<RecordingHandler>();
        services.AddMuninExplorer(options => options.ApiBaseUrl = LiveApi.BaseUrl);

        // Attached to the pipeline the client already has rather than to one of our own, so the
        // recording sits behind the handlers a host would run and sees what the client sees.
        var client = services.AddHttpClient(LiveApi.ClientName).AddHttpMessageHandler<RecordingHandler>();

        if (answeredBy is not null)
        {
            client.ConfigurePrimaryHttpMessageHandler(() => answeredBy);
        }

        var provider = services.BuildServiceProvider();

        return new LiveApiConnection(provider, provider.GetRequiredService<ResponseLog>());
    }

    /// <summary>
    /// Makes one call and fails the test if what came back does not survive a round trip through
    /// the contracts unchanged in shape.
    /// </summary>
    /// <remarks>
    /// The call is passed in rather than made by the caller so the response being compared is
    /// unambiguously the response that call produced. Tests are free to make other calls — finding
    /// an id to ask about, usually — outside this method without confusing it.
    /// </remarks>
    public async Task<T> RoundTripAsync<T>(Func<IMuninExplorerClient, Task<T>> call)
    {
        ArgumentNullException.ThrowIfNull(call);

        log.Clear();

        var value = await call(Client);

        if (log.Responses.Count != 1)
        {
            Assert.Fail(
                $"Expected the call to make exactly one request; it made {log.Responses.Count}. " +
                $"Zero means nothing was recorded, which means {nameof(RecordingHandler)} is not in the " +
                $"pipeline of the client named '{LiveApi.ClientName}' — check that name against what " +
                "AddHttpClient<IMuninExplorerClient, MuninExplorerClient> registers.");
        }

        var response = log.Responses[0];

        if (response.Status != HttpStatusCode.OK)
        {
            // Not folded in with the drift findings: a 404 deserialises to a default DTO that would
            // round-trip against an empty body and look like a clean pass. The endpoint moving is
            // drift too, and this is where it shows up.
            Assert.Fail($"{response.Uri} answered {(int)response.Status} {response.Status}, so there is no response to check.");
        }

        var drift = ShapeDrift.Against(response.Body, value);

        if (drift.Count > 0)
        {
            Assert.Fail(Explain(response.Uri, drift));
        }

        return value;
    }

    public void Dispose() => provider.Dispose();

    private static string Explain(Uri? uri, IReadOnlyList<string> drift) =>
        $"""
         {uri} no longer matches Fhi.Munin.Explorer.Contracts — {drift.Count} difference(s):

         {string.Join(Environment.NewLine + Environment.NewLine, drift.Select(finding => "  * " + finding))}

         The API and this package are released separately, so this is the build that notices.
         Update the DTO under src/Fhi.Munin.Explorer.Contracts, re-capture the matching file under
         test/Fhi.Munin.Explorer.Tests/Testdata/ so the offline ContractCoverageTest agrees with the
         same payload, and write a changelog fragment — a contract change is one every host sees.
         """;
}
