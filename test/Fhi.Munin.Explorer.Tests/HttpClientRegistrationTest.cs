using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// What <c>AddMuninExplorer</c> composes, read back off the container rather than off the helper
/// that builds it — one returning the right handler proves nothing if nothing installs it.
/// </summary>
public class HttpClientRegistrationTest
{
    /// <summary>The name <c>AddHttpClient&lt;TClient, TImplementation&gt;</c> registers under.</summary>
    private const string ClientName = nameof(IMuninExplorerClient);

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();

        services.AddMuninExplorer(o => o.ApiBaseUrl = "https://runa.munin.skytest.fhi.no");

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

        var sockets = Assert.IsType<SocketsHttpHandler>(primary);

        Assert.Equal(TimeSpan.FromSeconds(5), sockets.ConnectTimeout);

        // Retiring connections is what re-resolves DNS. Left unset, that fell to the factory
        // discarding the handler every two minutes, which is not a schedule anyone here chose.
        Assert.Equal(TimeSpan.FromSeconds(30), sockets.PooledConnectionLifetime);
    }

    [Fact]
    public void Registration_WhenNoBaseUrlWasSupplied_ThenTheStartupFailureNamesAHostThatAnswersOffTheFhiNetwork()
    {
        // The one string a host developer reads at the moment they are looking for the value, so
        // it is the likeliest thing here to be copied verbatim (Fhi.Metadata-ip02g).
        var services = new ServiceCollection();

        var thrown = Assert.Throws<InvalidOperationException>(
            () => services.AddMuninExplorer(o => o.ApiBaseUrl = null));

        Assert.Contains("https://runa.munin.skytest.fhi.no", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "https://munin.skytest.fhi.no", // internal-host-on-purpose
            thrown.Message,
            StringComparison.Ordinal);
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
            new Uri("https://runa.munin.skytest.fhi.no/api/explorer/variables"),
            new Uri(client.BaseAddress!, "api/explorer/variables"));
    }
}
