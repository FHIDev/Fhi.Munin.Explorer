namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// The API refused the request because too many arrived too fast — HTTP 429.
/// </summary>
/// <remarks>
/// Its own type rather than the <see cref="HttpRequestException"/> every other non-2xx answer
/// arrives as, because this one is not a fault: the API is up and answering, and what the reader
/// has to be told is that they — or, on a shared address, the whole site they are reading from —
/// asked too often. A caller that cannot tell the two apart says "try again shortly" to a reader
/// whose only problem is that trying again immediately is what caused this.
/// <para>
/// Thrown rather than mapped to null or to an empty result, which is the tempting shape because
/// the 404-to-null branch sits right beside it in the client. A throttled search turned into a
/// page of no rows tells the reader their search found nothing, for a search that was never run,
/// and takes the throttling out of sight of everything that logs on exceptions.
/// </para>
/// <para>
/// Nothing in this package retries on it, and nothing here waits for <see cref="RetryAfter"/>.
/// The API counts the limit per address and helsedata's cluster reaches it as one, so an automatic
/// retry would fire every reader's component at the same instant and rebuild the same burst
/// against the same window, round after round. Waiting is the reader's to do.
/// </para>
/// <para>
/// In <see cref="Fhi.Munin.Explorer.Contracts"/> rather than beside the HTTP client that raises
/// it: which statuses mean what is part of what a caller of <see cref="IMuninExplorerClient"/> has
/// to know, and a host substituting its own implementation throws this for the same reason ours
/// does. The components catch it and never name an HTTP type.
/// </para>
/// </remarks>
public sealed class MuninExplorerRateLimitedException(TimeSpan? retryAfter = null)
    : Exception("The Munin Explorer API answered 429 Too Many Requests.")
{
    /// <summary>
    /// How long the API asked the caller to wait, or null when it did not say.
    /// </summary>
    /// <remarks>
    /// Read off <c>Retry-After</c> in either form that header takes — a number of seconds, or an
    /// HTTP date, converted to the wait it implies. Null is an ordinary answer: the header is
    /// optional and a proxy in front of the API can drop it, so a caller logging this has to be
    /// able to print "unknown" rather than a zero that reads like "go now".
    /// <para>
    /// Carried, not rendered. A number on the page invites the reader to watch it, and a countdown
    /// is a promise this package cannot keep — the window is shared, so the moment it names may
    /// already be spent by somebody else's request. The reader is told to wait a little, which is
    /// true whatever the header said.
    /// </para>
    /// </remarks>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
