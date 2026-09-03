category: Notes for hosts
- **Shareable search links now need no host code at all.** `VariableExplorer` and
  `KildeExplorerWithUrlState` read the query the page was opened with and write it back themselves,
  so the wrapper component, the parsing and the `history.replaceState` a host used to copy out of
  our samples are gone. Both mount at an **interactive render mode only** — `render-mode="Server"`,
  never `ServerPrerendered` — and now **throw on initialisation** rather than rendering a page whose
  URL silently never follows the view. `KildeExplorerWithUrlState` takes `VariableExplorerPath`
  instead of a handover callback, because a delegate from a statically rendered parent arrives
  empty; it is relative to your application, so a path base survives it.
  <br><br>
  Your own parameters survive: each component rewrites only the keys it owns and carries everything
  else through untouched, `?utm_source=` included. `DeclinedKeys` keeps one of ours as well.
- **`ExplorerUrlState.QueryKeys` now names the filter's parameters too.** It listed only `search`,
  `sort`, `sortDir`, `page` and `pageSize`, while `ToQueryString` also writes `kildeIds` and the
  other facets — so a host using it to tell our parameters from its own kept those as its own and
  wrote them a second time. `ExplorerUrlState.ScalarQueryKeys` is the old five, and the set a
  component will let you decline.
