category: Notes for hosts
- `AddMuninExplorer` now calls `AddLogging`. It is idempotent and `TryAdd`-based inside, so a host
  that already configured logging keeps every provider, filter and minimum level it set, and a host
  that configured none gets the default factory rather than a component that cannot report a fault.
  The categories to filter on are `Fhi.Munin.Explorer.Blazor.*`, `Fhi.Munin.Explorer.Client.*` and
  `Fhi.Munin.Explorer.State.*`, and the highest level written is `Error`. `Error` means a fault:
  a throttled request and a request the API declined to accept the caller for are `Warning`, on
  the saved-list paths as much as anywhere else, so a host whose token provider is misconfigured
  fills the `Warning` channel rather than the `Error` one. A host that mounts a
  component without calling `AddMuninExplorer` at all still renders: the logger is resolved
  optionally, and the components no-op when there is none, and a provider that throws is caught at
  that same seam rather than reaching the page. Message templates no longer repeat the component
  name the category carries, so filter and group on the category.
