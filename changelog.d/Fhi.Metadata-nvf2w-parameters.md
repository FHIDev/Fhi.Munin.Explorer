category: Added
- **`KildeSearch` takes `@bind-Search`, `@bind-FacetChoices`, `@bind-TickedKildeIds` and
  `@bind-VisibleColumns`, beside `@bind-Order`.** Each is read once when the list opens and raised
  on every change, so a host that mounts `KildeSearch` itself can keep the list's state in its own
  address as `KildeExplorer` does. `KildeSearch.FacetKeys` and `KildeSearch.ColumnKeys` name the
  values. A host that binds none of them sees no change. (Fhi.Metadata-nvf2w)
