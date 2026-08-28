using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The seam that lets an embedding host call Munin on behalf of its signed-in user.
/// Anonymous browsing is the default and must stay working, so most of what matters here
/// is what happens when no host supplies a token.
/// </summary>
public class BearerTokenHandlerTest
{
    private const string BaseAddress = "https://munin.skytest.fhi.no/";

    private sealed class FixedTokenProvider(string? token) : IMuninExplorerTokenProvider
    {
        public int Calls { get; private set; }

        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(token);
        }
    }

    private static HttpClient WithProvider(IMuninExplorerTokenProvider provider, StubHttpHandler inner) =>
        new(new BearerTokenHandler(provider) { InnerHandler = inner }) { BaseAddress = new Uri(BaseAddress) };

    [Fact]
    public async Task SendAsync_WhenTheHostSuppliesAToken_ThenItIsSentAsBearer()
    {
        var inner = StubHttpHandler.Ok("{}");
        var client = WithProvider(new FixedTokenProvider("a-token"), inner);

        await client.GetAsync("api/explorer/variables");

        Assert.Equal("Bearer", inner.LastAuthorization?.Scheme);
        Assert.Equal("a-token", inner.LastAuthorization?.Parameter);
    }

    [Fact]
    public async Task SendAsync_WhenTheHostSuppliesNoToken_ThenNoAuthorizationHeaderIsSent()
    {
        // The common case: a signed-out visitor browsing public metadata. Returning null is
        // a normal answer, not a failure, and must not turn into an empty Bearer header.
        var inner = StubHttpHandler.Ok("{}");
        var client = WithProvider(new FixedTokenProvider(null), inner);

        await client.GetAsync("api/explorer/variables");

        Assert.Null(inner.LastAuthorization);
    }

    [Fact]
    public async Task SendAsync_WhenTheHostSuppliesAnEmptyString_ThenNoAuthorizationHeaderIsSent()
    {
        var inner = StubHttpHandler.Ok("{}");
        var client = WithProvider(new FixedTokenProvider("   "), inner);

        await client.GetAsync("api/explorer/variables");

        Assert.Null(inner.LastAuthorization);
    }

    [Fact]
    public async Task SendAsync_WhenTheTokenHasWhitespaceAroundIt_ThenItIsTrimmed()
    {
        // A provider reading the token from configuration or a file easily hands back a
        // trailing newline. Sending that verbatim produces a header the API rejects.
        var inner = StubHttpHandler.Ok("{}");
        var client = WithProvider(new FixedTokenProvider("  a-token\n"), inner);

        await client.GetAsync("api/explorer/variables");

        Assert.Equal("a-token", inner.LastAuthorization?.Parameter);
    }

    [Fact]
    public async Task SendAsync_ForEveryRequest_ThenTheProviderIsAsked()
    {
        // Tokens expire, and IHttpClientFactory caches this pipeline across callers for
        // minutes. Asking once and caching the answer in the handler would serve a stale
        // token — or worse, one user's token to the next.
        var provider = new FixedTokenProvider("a-token");
        var inner = StubHttpHandler.Ok("{}");
        var client = WithProvider(provider, inner);

        await client.GetAsync("api/explorer/variables");
        await client.GetAsync("api/explorer/kilder");

        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task AddMuninExplorer_WhenTheHostRegistersNothing_ThenTheCallsAreAnonymous()
    {
        // Regression guard for v1: the explorer is public and read-only, and adding this
        // seam must not have made it start demanding a token.
        var services = new ServiceCollection();
        services.AddMuninExplorer(o => o.ApiBaseUrl = BaseAddress);

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMuninExplorerTokenProvider>();

        Assert.Null(await provider.GetTokenAsync());
    }

    [Fact]
    public async Task AddMuninExplorer_WhenTheHostRegistersItsOwnProviderFirst_ThenTheHostsWins()
    {
        // TryAdd means registration order matters, and the host has to go first. Worth
        // pinning: if this inverts, a host that thinks it wired up authentication would
        // silently keep calling anonymously.
        var services = new ServiceCollection();
        services.AddSingleton<IMuninExplorerTokenProvider>(new FixedTokenProvider("the-hosts-token"));
        services.AddMuninExplorer(o => o.ApiBaseUrl = BaseAddress);

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMuninExplorerTokenProvider>();

        Assert.Equal("the-hosts-token", await provider.GetTokenAsync());
    }

    [Fact]
    public async Task SendAsync_WhenTheRequestIsAlreadyAuthenticated_ThenItIsLeftAlone()
    {
        var inner = StubHttpHandler.Ok("{}");
        var client = WithProvider(new FixedTokenProvider("our-token"), inner);

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/explorer/variables");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "the-hosts-own");

        await client.SendAsync(request);

        Assert.Equal("the-hosts-own", inner.LastAuthorization?.Parameter);
    }
}
