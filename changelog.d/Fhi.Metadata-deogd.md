category: Fixed
- **The open variable is in the address bar, so a link to one can be shared.** Opening a variable
  writes `?variabelId=` and closing it removes the key again; a link opens that variable with the
  search, facets, sort and page around it intact. `ExplorerUrlState` gained a matching
  `SelectedVariableId`. (Fhi.Metadata-deogd)
