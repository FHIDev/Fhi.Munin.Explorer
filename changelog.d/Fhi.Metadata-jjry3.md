category: Added

- **`VariableListState` holds the signed-in reader's variable lists for the circuit** - one scoped
  service over six of the seven `my/lists` client methods, so the save action in the result list,
  the list view and the download all read and write the same copy and are told when one of them
  changes it. `GetMyListVariablesAsync` is deliberately not wrapped: it is a paged read of one
  list's contents, and paging state belongs to the surface showing it rather than to a holder shared
  by three of them.
  Scoped and never singleton: a singleton would be one reader's lists served to every circuit on the
  server. (Fhi.Metadata-jjry3)
- **`VariableExplorer` gains an `IsAuthenticated` parameter, defaulting to signed out.** Whether the
  reader is signed in is told by the host rather than discovered by calling `my/lists` and reading a
  401: probing spends a failed request per render on every signed-out reader, and cannot tell "no
  session" from "expired token" or "Munin is down". The default matters as much as the mechanism - a
  host that forgets the parameter loses saved lists, which somebody notices, where the other default
  would send unauthorised calls on every render, which nobody does.
- **Signed out, not one call reaches `my/lists`** - the guard sits in the holder rather than at each
  call site, so a surface added later cannot forget it. The test asserts on the number of calls that
  reached a counting client, not on what the page shows, because an implementation that calls and
  swallows the 401 looks identical on screen.
- **The holder is resolved with `GetService`, not `[Inject]`**, so a host that renders the explorer
  without calling `AddMuninExplorer` still gets an explorer and merely loses saved lists - the same
  tolerance the package already extends to a host with no localisation services registered.
