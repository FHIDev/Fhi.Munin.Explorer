category: Notes for hosts
- A host can now call Munin on behalf of its signed-in user: register an
  `IMuninExplorerTokenProvider` **before** `AddMuninExplorer`, and every request carries that
  token as `Authorization: Bearer`. With no provider registered nothing changes — calls stay
  anonymous, which is all public metadata browsing needs.
- Implementations must be resolvable from a singleton and must fetch the token *per call*.
  `IHttpClientFactory` caches the handler pipeline across callers for minutes, so a captured
  scoped dependency would serve a stale token, or one user's token to the next. In an
  interactive Blazor Server host that also rules out `IHttpContextAccessor`: there is no
  `HttpContext` during circuit activity.
