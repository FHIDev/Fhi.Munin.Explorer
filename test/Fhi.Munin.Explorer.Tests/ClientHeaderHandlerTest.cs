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
public class ClientHeaderHandlerTest
{
    /// <summary>What the handler should be sending, derived the way the handler derives it.</summary>
    private static string ExpectedValue()
    {
        var version = typeof(MuninExplorerOptions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        var plus = version.IndexOf('+');

        return "blazor/" + (plus >= 0 ? version[..plus] : version);
    }

    private static HttpClient WithHandler(StubHttpHandler inner) =>
        new(new ClientHeaderHandler { InnerHandler = inner }) { BaseAddress = new Uri("https://runa.munin.skytest.fhi.no/") };

    [Fact]
    public async Task SendAsync_WhenARequestIsSent_ThenTheClientHeaderTravelsWithIt()
    {
        var stub = StubHttpHandler.Status(HttpStatusCode.NotFound);

        await WithHandler(stub).GetAsync("api/explorer/kilder");

        Assert.Equal([ExpectedValue()], stub.LastClientHeader);
    }

    [Fact]
    public async Task SendAsync_WhenARequestIsSent_ThenTheVersionIsNotEmpty()
    {
        var stub = StubHttpHandler.Status(HttpStatusCode.NotFound);

        await WithHandler(stub).GetAsync("api/explorer/kilder");

        var value = Assert.Single(stub.LastClientHeader);
        Assert.StartsWith("blazor/", value, StringComparison.Ordinal);
        Assert.NotEqual("blazor/", value);
        Assert.NotEqual("blazor/ukjent", value);
    }

    [Theory]
    // No build currently stamps a sha, but a release pipeline or SourceLink would — the header
    // must not gain a label value per commit the day that changes.
    [InlineData("0.1.0+9f2c1ab", "0.1.0")]
    [InlineData("1.0.0-beta.2+9f2c1ab", "1.0.0-beta.2")]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    public void NormalizeVersion_WhenTheVersionHasBuildMetadata_ThenOnlyTheVersionPartIsKept(
        string raw, string expected)
    {
        Assert.Equal(expected, ClientHeaderHandler.NormalizeVersion(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+9f2c1ab")]
    [InlineData("(/)")]
    public void NormalizeVersion_WhenTheVersionYieldsNothingUsable_ThenTheUnknownLabelIsUsed(string? raw)
    {
        // "ukjent" is a real label Munin can group by; an empty or unsendable value is not.
        Assert.Equal("ukjent", ClientHeaderHandler.NormalizeVersion(raw));
    }

    [Fact]
    public void NormalizeVersion_WhenTheVersionHasCharactersThatDoNotBelongInAHeader_ThenTheyAreRemoved()
    {
        // A header value has to survive being sent; a stray space or slash must not make the
        // request the thing that fails.
        Assert.Equal("1.0.0rc1", ClientHeaderHandler.NormalizeVersion("1.0.0 rc/1"));
    }

    [Fact]
    public async Task SendAsync_WhenTheRequestAlreadyCarriesTheHeader_ThenItIsNotSentTwice()
    {
        // A retry handler resends the same request message; two values would break a group-by.
        var stub = StubHttpHandler.Status(HttpStatusCode.NotFound);
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/explorer/kilder");
        request.Headers.TryAddWithoutValidation(ClientHeaderHandler.Header, "blazor/already-set");

        await WithHandler(stub).SendAsync(request);

        Assert.Equal(["blazor/already-set"], stub.LastClientHeader);
    }

    [Fact]
    public async Task AddMuninExplorer_WhenTheClientIsResolvedFromDi_ThenTheHeaderIsAlreadyInPlace()
    {
        // The registration is the part that matters in production — a handler nobody wires up
        // measures nothing.
        var stub = StubHttpHandler.Ok("[]");
        var services = new ServiceCollection();

        services.AddMuninExplorer(o => o.ApiBaseUrl = "https://runa.munin.skytest.fhi.no");
        services.AddHttpClient<IMuninExplorerClient, MuninExplorerClient>()
                .ConfigurePrimaryHttpMessageHandler(() => stub);

        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMuninExplorerClient>().GetKilderAsync();

        Assert.Equal([ExpectedValue()], stub.LastClientHeader);
    }
}
