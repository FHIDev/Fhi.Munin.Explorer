category: Added

- **The Variabelliste tab has a filter panel of its own: the kilder the reader's list draws from,
  each with a count, and a tick that narrows the rows.** `VariableSearch` gained
  `VariableListFilters`, a second `RenderFragment` beside `VariableList`, drawn in the filter column
  while that tab is open and in place of the search's own facets. `VariableExplorer` passes the new
  `VariableListFilters` component there; a host composing its own page must pass it too, or that
  tab keeps the empty filter column it has today. The tally is built over the whole list, never the
  page on screen, and the narrowing is the API's — so the pager and the row count stay its answer.
- **`IMuninExplorerClient.GetMyListVariablesAsync` takes an optional `kildeIds`.** Several of them
  union rather than intersect, and null or empty is every kilde. A host implementing the interface
  rather than consuming `MuninExplorerClient` must widen its own signature. **Requires Munin API
  5658 or later**: an older API does not know the parameter, ignores it, and answers with the whole
  list.
