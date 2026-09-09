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
/// <para>
/// 401 and 403 are one case here rather than two because the API's my/lists authorization policy
/// has exactly one requirement — authenticated or not, no roles, no claims — so a request that
/// fails it has always failed authentication, never a permission check on an identity that exists;
/// list ownership is enforced separately and answers 404, not 403, precisely so it cannot be
/// mistaken for one (see the remarks on the Munin API's <c>MyListsController</c>). A 403 that
/// reaches this client, if the API's authorization model ever grows a real permission tier, would
/// need a case of its own — this one is for "no usable identity", not "identity without permission".
/// </para>
/// </remarks>
public sealed class MuninExplorerUnauthorizedException()
    : Exception("The Munin Explorer API answered a my/lists call as unauthenticated.");
