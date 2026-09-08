namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// A my/lists call answered as unauthenticated — HTTP 401 or 403.
/// </summary>
/// <remarks>
/// Its own type for the same reason <see cref="MuninExplorerRateLimitedException"/> has one: a
/// caller that read this as the general failure would tell a signed-out reader to try again, which
/// can never succeed, because a host can declare <c>IsAuthenticated</c> true while its token
/// provider sends nothing the API accepts. The RCL cannot verify that claim, only the API's answer
/// to it — so a component catches this rather than re-reading <c>IsAuthenticated</c>.
/// </remarks>
public sealed class MuninExplorerUnauthorizedException()
    : Exception("The Munin Explorer API answered a my/lists call as unauthenticated.");
