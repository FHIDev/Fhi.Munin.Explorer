using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    /// <summary>Registers the explorer's data client.</summary>
    /// <param name="konfigurer">Sets at least <see cref="MuninExplorerOptions.ApiBaseUrl"/>.</param>
    public static IServiceCollection AddMuninExplorer(
        this IServiceCollection services,
        Action<MuninExplorerOptions> konfigurer)
    {
        ArgumentNullException.ThrowIfNull(konfigurer);

        var options = new MuninExplorerOptions();
        konfigurer(options);

        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl))
        {
            // Fail at startup with a message that says what to set. The alternative is a
            // host that boots happily and renders an empty explorer in production.
            throw new InvalidOperationException(
                $"{nameof(MuninExplorerOptions)}.{nameof(MuninExplorerOptions.ApiBaseUrl)} must be set — " +
                "the base URL of the Munin API, e.g. https://munin.skytest.fhi.no");
        }

        services.AddHttpClient<IMuninExplorerClient, MuninExplorerClient>(client =>
        {
            // Trailing slash so relative routes ("api/explorer/...") resolve against the
            // base address instead of replacing its last segment.
            client.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
        });

        return services;
    }

    /// <summary>
    /// Reads <c>MuninExplorer:ApiBaseUrl</c> from configuration
    /// (environment variable <c>MuninExplorer__ApiBaseUrl</c>).
    /// </summary>
    public static IServiceCollection AddMuninExplorer(
        this IServiceCollection services,
        IConfiguration configuration,
        string? utviklingsFallback = null)
    {
        var fraKonfig = configuration["MuninExplorer:ApiBaseUrl"];
        return services.AddMuninExplorer(o =>
            o.ApiBaseUrl = string.IsNullOrWhiteSpace(fraKonfig) ? utviklingsFallback : fraKonfig);
    }
}
