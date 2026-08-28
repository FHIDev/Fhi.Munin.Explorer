using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// What <c>AddMuninExplorer</c> actually composes, read back off the container.
/// </summary>
/// <remarks>
/// Resolved out of the container, because a helper returning the right handler proves nothing if
/// the registration never reaches for it. (Fhi.Metadata-phgeg)
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
        // Walks the composed chain, because HttpClient.Timeout cannot shorten a connect the OS is
        // still retrying, and the limit that can is only in force if the registration installs it.
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
