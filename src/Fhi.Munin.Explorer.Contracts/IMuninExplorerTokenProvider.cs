namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Supplies the access token the explorer should call the Munin API with, on behalf of
/// the user signed in to the host application.
/// </summary>
/// <remarks>
/// <para>
/// The explorer is anonymous by default and stays that way unless a host registers an
/// implementation: public metadata needs no token. A token only matters for endpoints
/// that own something on the user's behalf, such as variable lists.
/// </para>
/// <para>
/// The host owns the token because the host owns the session. On helsedata.no the
/// component runs server-side inside their application, which already holds an ID-porten
/// session, and forwards that access token unchanged — the same thing their own backend
/// calls do.
/// </para>
/// <para>
/// <b>Implementations must be safe to resolve from a singleton.</b>
/// <c>IHttpClientFactory</c> builds and caches the message-handler pipeline in its own
/// scope, and reuses it across every caller for a couple of minutes, so a handler cannot
/// capture anything scoped. Resolve whatever carries the current user *inside*
/// <see cref="HentTokenAsync"/>, per call, rather than holding it in a field. In an
/// interactive Blazor Server host that specifically means <b>not</b> reaching for
/// <c>IHttpContextAccessor</c>: there is no <c>HttpContext</c> during circuit activity,
/// which arrives over a WebSocket. Use the circuit's own service provider instead.
/// </para>
/// </remarks>
public interface IMuninExplorerTokenProvider
{
    /// <summary>
    /// Returns the access token for the current user, or <c>null</c> to call anonymously.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Return the raw token value only — <c>eyJhbGci...</c>, not <c>Bearer eyJhbGci...</c>.
    /// The scheme is added when the header is written, so a returned prefix would be sent
    /// twice and the API would reject the call.
    /// </para>
    /// <para>
    /// Returning <c>null</c> is a normal answer, not a failure: a signed-out visitor
    /// browsing public metadata is the common case. Callers must not throw on it.
    /// </para>
    /// </remarks>
    Task<string?> HentTokenAsync(CancellationToken cancellationToken = default);
}
