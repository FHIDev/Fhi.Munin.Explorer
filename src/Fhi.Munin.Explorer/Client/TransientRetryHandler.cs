using System.Net.Sockets;

namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// Sends a read-only request once more when the connection under it died before answering.
/// </summary>
/// <remarks>
/// A pooled connection can be dead without anything having said so: the network goes away, the
/// sockets stay in the pool, and the next request is written into one and fails on the read —
/// "an existing connection was forcibly closed", after seventeen seconds of TCP retransmission.
/// No connect happens on that path, so no connect timeout can shorten it, and .NET's own retry
/// does not cover it either: that one repeats a request the connection refused before it was
/// sent, and this connection accepted it and then went silent. The failure evicts the connection,
/// so the second attempt opens a fresh one — which is what a reader does by hand when they press
/// the button again. (Fhi.Metadata-phgeg)
/// <para>
/// Only GET and HEAD. A reset arriving during the response read says nothing about whether the
/// server processed the request, so repeating a save or a delete could do it twice; repeating a
/// read cannot. Once, not until it works: a second failure is the network being down rather than
/// one stale connection, and the reader is told rather than kept waiting through another attempt.
/// </para>
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
    /// <remarks>
    /// A response that arrived is not retried whatever it says: a 500 is the server's answer and
    /// repeating it asks a working server the same question twice.
    /// </remarks>
    private static bool IsDeadConnection(HttpRequestException ex) =>
        ex.InnerException is IOException or SocketException;

    /// <summary>Whether repeating the request cannot do anything twice.</summary>
    private static bool IsSafeToRepeat(HttpRequestMessage request) =>
        request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
}
