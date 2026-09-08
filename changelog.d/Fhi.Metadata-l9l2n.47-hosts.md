category: Notes for hosts
- `AddMuninExplorer` now calls `AddLogging`. It is idempotent and `TryAdd`-based inside, so a host
  that already configured logging keeps every provider, filter and minimum level it set, and a host
  that configured none gets the default factory rather than a component that cannot report a fault.
  The categories to filter on are `Fhi.Munin.Explorer.Blazor.*`, `Fhi.Munin.Explorer.Client.*` and
  `Fhi.Munin.Explorer.State.*`, and the highest level written is `Error`. A host that mounts a
  component without calling `AddMuninExplorer` at all still renders: the logger is resolved
  optionally, and the components no-op when there is none.
