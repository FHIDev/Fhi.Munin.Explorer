category: Notes for hosts
- The package now ships its XML documentation, so the rules that matter show up in IntelliSense
  at the call site rather than only in this repository — including that an
  `IMuninExplorerTokenProvider` must be singleton-safe and must not reach for
  `IHttpContextAccessor`.
- The package carries a real description on the feed rather than the packer's placeholder, so it
  says what it is to someone deciding whether to install it.
