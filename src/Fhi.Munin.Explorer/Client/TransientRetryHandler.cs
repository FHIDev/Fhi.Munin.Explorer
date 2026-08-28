using System.Net.Sockets;

namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// Sends a read-only request once more when the connection under it died before answering.
/// </summary>
/// <remarks>
/// A connection can die in the pool with nothing having said so, and the request written into it
/// then fails on the read rather than on a connect — which no connect timeout bounds and .NET's
/// own retry does not cover. (Fhi.Metadata-phgeg)
/// </remarks>
internal sealed class TransientRetryHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (IsDeadConnection(ex) && IsSafeToRepeat(request))
        {
            // Not on a cancelled token: the reader navigated away, or HttpClient.Timeout fired,
            // and a retry would spend the same wait again on a request nobody is waiting for.
            cancellationToken.ThrowIfCancellationRequested();

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Whether the transport failed, as opposed to the server answering something.</summary>
    private static bool IsDeadConnection(HttpRequestException ex) =>
        ex.InnerException is IOException or SocketException;

    /// <summary>Whether repeating the request cannot do anything twice.</summary>
    private static bool IsSafeToRepeat(HttpRequestMessage request) =>
        request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
}
