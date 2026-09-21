category: Notes for hosts
- **BREAKING for hosts mounting `DatasamlingView` directly: it now needs `AddMuninExplorer`.** The
  view fetches the variable table itself, so an `IMuninExplorerClient` has to be registered before
  it renders — the same requirement `KildeView` has carried since it began loading its hierarchy.
  Hosts that mount `VariableExplorer`, `KildeExplorer`, `VariableSearch` or `KildeSearch` already
  meet it and need do nothing. Two class names come with it and
  `Fhi.Helsedata.Stiler` carries a rule for neither in any published version yet:
  `munin-explorer-datasamling__variabler` on the `<table>`, and
  `munin-explorer-datasamling__variabler-tom` on the paragraph that replaces it for a collection
  with no variables. Both are handles — the table is a real `<table>` with `<th scope="col">` per
  column and `<th scope="row">` on the code, and the empty case is a paragraph — so an undefined
  one costs borders, padding and column widths rather than structure or words. Both sample
  stylesheets show a stand-in; the Stiler rules are `Fhi.Metadata-w4lys`. The loading, failure and
  pagination controls beside them add no name: they are the ones the result list already writes.
  (Fhi.Metadata-mg08i)
