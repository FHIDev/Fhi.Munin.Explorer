using System.Net;
using System.Reflection;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The client-identification header. Munin's API is anonymous, so this header is the only thing
/// that tells its dashboards which traffic is the embedded component and which version of it.
/// </summary>
public class KlientHeaderHandlerTest
{
    /// <summary>What the handler should be sending, derived the way the handler derives it.</summary>
    private static string ForventetVerdi()
    {
        var versjon = typeof(MuninExplorerOptions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        var pluss = versjon.IndexOf('+');

        return "blazor/" + (pluss >= 0 ? versjon[..pluss] : versjon);
    }

    private static HttpClient MedHandler(StubbetHttpHandler ytre) =>
        new(new KlientHeaderHandler { InnerHandler = ytre }) { BaseAddress = new Uri("https://munin.skytest.fhi.no/") };

    [Fact]
    public async Task SendAsync_NårForespørselenSendes_ThenFølgerKlientheaderMed()
    {
        var stub = StubbetHttpHandler.Status(HttpStatusCode.NotFound);

        await MedHandler(stub).GetAsync("api/explorer/kilder");

        Assert.Equal([ForventetVerdi()], stub.SisteKlientheader);
    }

    [Fact]
    public async Task SendAsync_NårForespørselenSendes_ThenErVersjonenIkkeTom()
    {
        var stub = StubbetHttpHandler.Status(HttpStatusCode.NotFound);

        await MedHandler(stub).GetAsync("api/explorer/kilder");

        var verdi = Assert.Single(stub.SisteKlientheader);
        Assert.StartsWith("blazor/", verdi, StringComparison.Ordinal);
        Assert.NotEqual("blazor/", verdi);
        Assert.NotEqual("blazor/ukjent", verdi);
    }

    [Theory]
    // No build currently stamps a sha, but a release pipeline or SourceLink would — the header
    // must not gain a label value per commit the day that changes.
    [InlineData("0.1.0+9f2c1ab", "0.1.0")]
    [InlineData("1.0.0-beta.2+9f2c1ab", "1.0.0-beta.2")]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    public void Versjon_NårVersjonenHarByggemetadata_ThenBeholdesBareVersjonsdelen(string raa, string forventet)
    {
        Assert.Equal(forventet, KlientHeaderHandler.Versjon(raa));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+9f2c1ab")]
    [InlineData("(/)")]
    public void Versjon_NårVersjonenIkkeGirNoeBrukbart_ThenBrukesUkjent(string? raa)
    {
        // "ukjent" is a real label Munin can group by; an empty or unsendable value is not.
        Assert.Equal("ukjent", KlientHeaderHandler.Versjon(raa));
    }

    [Fact]
    public void Versjon_NårVersjonenHarTegnSomIkkeHørerHjemmeIEnHeader_ThenFjernesDe()
    {
        // A header value has to survive being sent; a stray space or slash must not make the
        // request the thing that fails.
        Assert.Equal("1.0.0rc1", KlientHeaderHandler.Versjon("1.0.0 rc/1"));
    }

    [Fact]
    public async Task SendAsync_NårForespørselenAlleredeHarHeaderen_ThenSendesDenIkkeToGanger()
    {
        // A retry handler resends the same request message; two values would break a group-by.
        var stub = StubbetHttpHandler.Status(HttpStatusCode.NotFound);
        using var forespørsel = new HttpRequestMessage(HttpMethod.Get, "api/explorer/kilder");
        forespørsel.Headers.TryAddWithoutValidation(KlientHeaderHandler.Header, "blazor/allerede-satt");

        await MedHandler(stub).SendAsync(forespørsel);

        Assert.Equal(["blazor/allerede-satt"], stub.SisteKlientheader);
    }

    [Fact]
    public async Task AddMuninExplorer_NårKlientenLøsesFraDI_ThenErHeaderenAlleredePåPlass()
    {
        // The registration is the part that matters in production — a handler nobody wires up
        // measures nothing.
        var stub = StubbetHttpHandler.Ok("[]");
        var services = new ServiceCollection();

        services.AddMuninExplorer(o => o.ApiBaseUrl = "https://munin.skytest.fhi.no");
        services.AddHttpClient<IMuninExplorerClient, MuninExplorerClient>()
                .ConfigurePrimaryHttpMessageHandler(() => stub);

        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMuninExplorerClient>().HentKilderAsync();

        Assert.Equal([ForventetVerdi()], stub.SisteKlientheader);
    }
}
