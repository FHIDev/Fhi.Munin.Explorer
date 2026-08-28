using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fhi.Munin.Explorer.Client;

/// <summary>Options a host supplies when registering the explorer.</summary>
public sealed class MuninExplorerOptions
{
    /// <summary>Base URL of the Munin API, e.g. <c>https://munin.skytest.fhi.no</c>.</summary>
    public string? ApiBaseUrl { get; set; }
}

/// <summary>The one call a host makes to use the explorer components.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>How long a call may take in total before the reader is told it failed.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long to spend reaching the host before giving up on it.</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    // ConnectTimeout is the limit that bites on an unreachable host: HttpClient.Timeout bounds the
    // whole request, so it is the ceiling and not the way out. Plain handler on browser, where
    // SocketsHttpHandler does not exist and fetch owns the connect. (Fhi.Metadata-phgeg)
    private static HttpMessageHandler PrimaryHandler()
    {
        if (OperatingSystem.IsBrowser())
        {
            return new HttpClientHandler();
        }

        return new SocketsHttpHandler { ConnectTimeout = ConnectTimeout };
    }

    /// <summary>Registers the explorer's data client.</summary>
    /// <remarks>
    /// Calls are anonymous unless the host has already registered its own
    /// <see cref="Contracts.IMuninExplorerTokenProvider"/>. Registration order decides it:
    /// register yours <em>before</em> this call, or the anonymous default wins and the
    /// explorer quietly keeps calling without a token.
    /// </remarks>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configure">Sets at least <see cref="MuninExplorerOptions.ApiBaseUrl"/>.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddMuninExplorer(
        this IServiceCollection services,
        Action<MuninExplorerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MuninExplorerOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl))
        {
            // Fail at startup with a message that says what to set. The alternative is a
            // host that boots happily and renders an empty explorer in production.
            throw new InvalidOperationException(
                $"{nameof(MuninExplorerOptions)}.{nameof(MuninExplorerOptions.ApiBaseUrl)} must be set — " +
                "the base URL of the Munin API, e.g. https://munin.skytest.fhi.no");
        }

        // TryAdd, so a host that wants real tokens registers its own provider BEFORE
        // calling AddMuninExplorer and wins. Registered as a singleton on purpose: the
        // handler pipeline below is built and cached by IHttpClientFactory in its own
        // scope and reused across callers, so nothing scoped may be captured in it.
        services.TryAddSingleton<IMuninExplorerTokenProvider, AnonymousTokenProvider>();

        services.AddTransient<ClientHeaderHandler>();
        services.AddTransient<TransientRetryHandler>();
        services.AddTransient<BearerTokenHandler>();

        // Scoped, so the surfaces sharing a circuit share one copy of the user's lists. Never
        // singleton: that would be one user's lists served to every circuit on the server.
        services.TryAddScoped<VariableListState>();

        services.AddHttpClient<IMuninExplorerClient, MuninExplorerClient>(client =>
        {
            // Trailing slash so relative routes ("api/explorer/...") resolve against the
            // base address instead of replacing its last segment.
            client.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");

            // Not the 100-second default, which is a number chosen for a background job and not
            // for someone waiting at a page. A healthy variable search answers in well under a
            // second; anything still running at thirty is not going to be read. (Fhi.Metadata-phgeg)
            client.Timeout = RequestTimeout;
        })
        .ConfigurePrimaryHttpMessageHandler(PrimaryHandler)
        // Identifies this component to Munin's observability — see ClientHeaderHandler.
        .AddHttpMessageHandler<ClientHeaderHandler>()
        // Attaches the host's user token when it supplies one. With no provider
        // registered the default supplies none and calls stay anonymous, which is what
        // public metadata browsing needs.
        .AddHttpMessageHandler<BearerTokenHandler>()
        // Innermost, so it repeats the network call and not the whole chain above it.
        .AddHttpMessageHandler<TransientRetryHandler>();

        return services;
    }

    /// <summary>
    /// Reads <c>MuninExplorer:ApiBaseUrl</c> from configuration
    /// (environment variable <c>MuninExplorer__ApiBaseUrl</c>).
    /// </summary>
    public static IServiceCollection AddMuninExplorer(
        this IServiceCollection services,
        IConfiguration configuration,
        string? developmentFallback = null)
    {
        var fromConfiguration = configuration["MuninExplorer:ApiBaseUrl"];
        return services.AddMuninExplorer(o =>
            o.ApiBaseUrl = string.IsNullOrWhiteSpace(fromConfiguration) ? developmentFallback : fromConfiguration);
    }
}
