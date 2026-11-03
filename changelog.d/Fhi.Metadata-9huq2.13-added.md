category: Added
- **`KildeSearch.ExploreSelectionRequested` hands over a marked selection as one `VariableFilter`.**
  It carries the ticked kilder, the marked datasamlinger and `OrTreeTicks`; pass
  `filter.ToQueryString()` to the variable explorer. A host that mounts `KildeSearch` itself and
  wires it gets the behaviour `KildeExplorer` now has. `KildeSearch.MaxHandoverQueryLength` (7168)
  is the longest query any selection may hand over, marked or not. (Fhi.Metadata-9huq2.13)
