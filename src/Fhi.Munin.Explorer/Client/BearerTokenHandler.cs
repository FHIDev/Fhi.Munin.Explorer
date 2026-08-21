using System.Net.Http.Headers;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// Attaches the host's access token to outgoing Munin API calls, when there is one.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately depends only on <see cref="IMuninExplorerTokenProvider"/>, which is
/// registered as a singleton. <c>IHttpClientFactory</c> builds this pipeline in its own
/// scope and caches it across every caller for roughly two minutes, so anything scoped
/// captured here would leak between users — in the worst case handing one person's token
/// to another. The provider is asked per request instead.
/// </para>
/// <para>
/// An existing <c>Authorization</c> header is never overwritten: if a host has wired its
/// own handler that already authenticated the request, that decision wins.
/// </para>
/// </remarks>
internal sealed class BearerTokenHandler(IMuninExplorerTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            var token = await tokenProvider.GetTokenAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(token))
            {
                // Trimmed because a provider that reads the token from configuration, a file
                // or an environment variable easily returns a trailing newline, and that would
                // travel into the header verbatim.
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// The default when no host registers one: never supplies a token, so the explorer calls
/// Munin anonymously exactly as it did before the seam existed.
/// </summary>
internal sealed class AnonymousTokenProvider : IMuninExplorerTokenProvider
{
    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
