using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// What <c>AddMuninExplorer</c> actually composes, read back off the container.
/// </summary>
/// <remarks>
/// The limits below are the reader's, not a tuning detail: without them a dropped network leaves
/// someone under a spinner for 12 to 33 seconds with nothing to press, which is what
/// <c>Fhi.Metadata-phgeg</c> was reported as. A helper returning the right handler proves nothing
/// if the registration never reaches for it, so these resolve the real chain.
/// </remarks>
public class HttpClientRegistrationTest
{
    /// <summary>The name <c>AddHttpClient&lt;TClient, TImplementation&gt;</c> registers under.</summary>
    private const string ClientName = nameof(IMuninExplorerClient);

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();

        services.AddMuninExplorer(o => o.ApiBaseUrl = "https://munin.skytest.fhi.no");

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Registration_WhenACallHangs_ThenItIsAbandonedLongBeforeTheHundredSecondDefault()
    {
        using var provider = Provider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    }

    [Fact]
    public void Registration_WhenTheHostIsUnreachable_ThenTheConnectGivesUpWithoutWaitingForTheWholeRequest()
    {
        // The one that answers the bead. HttpClient.Timeout bounds the whole request and so cannot
        // shorten a connect the OS is still retrying — measured at 12 s and 33 s against a dropped
        // network. This walks the composed chain rather than calling the factory method, because
        // what broke was reachable only if the registration actually installs it.
        using var provider = Provider();

        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(ClientName);

        var primary = handler;
        while (primary is DelegatingHandler delegating && delegating.InnerHandler is not null)
        {
            primary = delegating.InnerHandler;
        }

        Assert.Equal(TimeSpan.FromSeconds(5), Assert.IsType<SocketsHttpHandler>(primary).ConnectTimeout);
    }

    [Fact]
    public void Registration_WhenTheHostSuppliedABaseUrl_ThenRelativeRoutesResolveUnderIt()
    {
        // Pinned beside the timeouts because both are set in the same lambda: a later edit that
        // rewrites one has to leave the other, and the trailing slash is what keeps
        // "api/explorer/..." from replacing the last segment of the base address.
        using var provider = Provider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

        Assert.Equal(
            new Uri("https://munin.skytest.fhi.no/api/explorer/variables"),
            new Uri(client.BaseAddress!, "api/explorer/variables"));
    }
}
