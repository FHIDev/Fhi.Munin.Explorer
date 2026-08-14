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
    public async Task SendAsync_NårForespørselenSendes_ThenErVersjonenUtenByggemetadata()
    {
        var stub = StubbetHttpHandler.Status(HttpStatusCode.NotFound);

        await MedHandler(stub).GetAsync("api/explorer/kilder");

        var verdi = Assert.Single(stub.SisteKlientheader);
        Assert.StartsWith("blazor/", verdi, StringComparison.Ordinal);

        // "+<commit sha>" would give Munin one label value per commit — a dashboard nobody can group by.
        Assert.DoesNotContain('+', verdi);
        Assert.NotEqual("blazor/", verdi);
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
