category: Changed

- **BREAKING for hosts: `KildeExplorer` is the whole kildeutforsker.** The kilde list, the drill-in
  and the open kilde in the address bar, from one mount — `Language` is all it takes. The list on
  its own, with no `?kilde=` handling, is now `KildeSearch`, and the separate
  `KildeExplorerWithUrlState` is gone — `KildeExplorer` is now the component that name used to
  mean, so mounting `KildeExplorer` now gets you the wrapper's behaviour rather than the old bare
  list. Both mounts still declare `ShowAccessAndPrices`, and the merged one still takes
  `VariableExplorerPath`. This is the same fold the variable side had in the previous release, so a
  host that upgrades once renames both.
