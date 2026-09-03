category: Added
- **`VariableExplorer` and `KildeExplorerWithUrlState` — the explorers with their state in your
  address bar, and no glue to write.** A link restores the search, the facets, the sort, the page
  and the open kilde; every change the reader makes updates the URL. `ExplorerUrlState` is still
  there for hosts that would rather own the address bar themselves.
- **`VariableFilter.QueryKeys`**, the facet half of what an explorer link carries. `DeclinedKeys`
  names query keys the explorer must leave alone, for a page that already means something else by
  `?page=`.
