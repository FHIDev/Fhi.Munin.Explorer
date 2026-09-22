category: Notes for hosts
- **`KildeExplorer` now owns `?selectedDatasamling=` on the page it is mounted on, and two new
  class names need rules.** `munin-explorer-kilde__datasamling-select` is the drawer's checkbox
  cell and `munin-explorer-kilde__datasamlinger--selectable` the modifier its table wears; both
  ship in `Fhi.Helsedata.Stiler` 0.1.99 and later. Neither is drawn without
  `VariableExplorerPath`. A host on an older Stiler gets the column at browser defaults, and — the
  half worth knowing — a table whose other four columns are each sized for the one beside them,
  because the modifier is where those positional rules are re-anchored. A host that means
  something else by `?selectedDatasamling=` on that page mounts `KildeSearch` and owns the query
  string itself. (Fhi.Metadata-75yov)
