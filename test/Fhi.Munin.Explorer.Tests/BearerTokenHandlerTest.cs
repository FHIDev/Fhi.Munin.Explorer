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
    private const string Basisadresse = "https://munin.skytest.fhi.no/";

    private sealed class FastTokenProvider(string? token) : IMuninExplorerTokenProvider
    {
        public int Kall { get; private set; }

        public Task<string?> HentTokenAsync(CancellationToken cancellationToken = default)
        {
            Kall++;
            return Task.FromResult(token);
        }
    }

    private static HttpClient MedProvider(IMuninExplorerTokenProvider provider, StubbetHttpHandler ytre) =>
        new(new BearerTokenHandler(provider) { InnerHandler = ytre }) { BaseAddress = new Uri(Basisadresse) };

    [Fact]
    public async Task SendAsync_NårVertenGirEtToken_ThenSendesDetSomBearer()
    {
        var ytre = StubbetHttpHandler.Ok("{}");
        var klient = MedProvider(new FastTokenProvider("et-token"), ytre);

        await klient.GetAsync("api/explorer/variables");

        Assert.Equal("Bearer", ytre.SisteAutorisasjon?.Scheme);
        Assert.Equal("et-token", ytre.SisteAutorisasjon?.Parameter);
    }

    [Fact]
    public async Task SendAsync_NårVertenIkkeGirNoeToken_ThenSendesIngenAuthorizationHeader()
    {
        // The common case: a signed-out visitor browsing public metadata. Returning null is
        // a normal answer, not a failure, and must not turn into an empty Bearer header.
        var ytre = StubbetHttpHandler.Ok("{}");
        var klient = MedProvider(new FastTokenProvider(null), ytre);

        await klient.GetAsync("api/explorer/variables");

        Assert.Null(ytre.SisteAutorisasjon);
    }

    [Fact]
    public async Task SendAsync_NårVertenGirTomStreng_ThenSendesIngenAuthorizationHeader()
    {
        var ytre = StubbetHttpHandler.Ok("{}");
        var klient = MedProvider(new FastTokenProvider("   "), ytre);

        await klient.GetAsync("api/explorer/variables");

        Assert.Null(ytre.SisteAutorisasjon);
    }

    [Fact]
    public async Task SendAsync_ForHverForespørsel_ThenSpørresProvideren()
    {
        // Tokens expire, and IHttpClientFactory caches this pipeline across callers for
        // minutes. Asking once and caching the answer in the handler would serve a stale
        // token — or worse, one user's token to the next.
        var provider = new FastTokenProvider("et-token");
        var ytre = StubbetHttpHandler.Ok("{}");
        var klient = MedProvider(provider, ytre);

        await klient.GetAsync("api/explorer/variables");
        await klient.GetAsync("api/explorer/kilder");

        Assert.Equal(2, provider.Kall);
    }

    [Fact]
    public async Task AddMuninExplorer_NårVertenIkkeRegistrererNoe_ThenErKalleneAnonyme()
    {
        // Regression guard for v1: the explorer is public and read-only, and adding this
        // seam must not have made it start demanding a token.
        var tjenester = new ServiceCollection();
        tjenester.AddMuninExplorer(o => o.ApiBaseUrl = Basisadresse);

        using var leverandør = tjenester.BuildServiceProvider();
        var provider = leverandør.GetRequiredService<IMuninExplorerTokenProvider>();

        Assert.Null(await provider.HentTokenAsync());
    }

    [Fact]
    public async Task AddMuninExplorer_NårVertenRegistrererEgenProviderFørst_ThenVinnerVertens()
    {
        // TryAdd means registration order matters, and the host has to go first. Worth
        // pinning: if this inverts, a host that thinks it wired up authentication would
        // silently keep calling anonymously.
        var tjenester = new ServiceCollection();
        tjenester.AddSingleton<IMuninExplorerTokenProvider>(new FastTokenProvider("vertens-token"));
        tjenester.AddMuninExplorer(o => o.ApiBaseUrl = Basisadresse);

        using var leverandør = tjenester.BuildServiceProvider();
        var provider = leverandør.GetRequiredService<IMuninExplorerTokenProvider>();

        Assert.Equal("vertens-token", await provider.HentTokenAsync());
    }

    [Fact]
    public async Task SendAsync_NårForespørselenAlleredeErAutentisert_ThenRøresDenIkke()
    {
        var ytre = StubbetHttpHandler.Ok("{}");
        var klient = MedProvider(new FastTokenProvider("vårt-token"), ytre);

        using var forespørsel = new HttpRequestMessage(HttpMethod.Get, "api/explorer/variables");
        forespørsel.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "hostens-eget");

        await klient.SendAsync(forespørsel);

        Assert.Equal("hostens-eget", ytre.SisteAutorisasjon?.Parameter);
    }
}
