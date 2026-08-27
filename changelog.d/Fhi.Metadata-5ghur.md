category: Added

- **`KildeExplorer` can hand a selection of kilder to the variable explorer.** A checkbox column,
  a velg-alle over the rows the reader can see, a `{n} kilder valgt` line and a *Nullstill utvalg*
  beside it — Kelda's own workflow, in the component. The new
  `ExploreVariablesRequested` (`EventCallback<IReadOnlyList<Guid>>`) is how the selection leaves:
  the component has no router and no idea where you mounted a `VariableExplorer`, so it tells you
  which kilder the reader chose and you decide where that goes.
  `new VariableFilter { KildeIds = ids }.ToQueryString()` writes the query
  `VariableExplorer.Filter` already reads, which is the whole of the pairing. (Fhi.Metadata-5ghur)
- **What travels is not always what is ticked**, and the three cases are Munin's own. Ticked rows
  win outright — a ticked kilde the current search has hidden still travels. With nothing ticked
  but a search or a facet in force, the rows on screen travel instead, because most of what Kelda
  filters on has no equivalent facet on the other side. With neither, the list is empty, which
  means the whole catalogue rather than a selection of none. (Fhi.Metadata-5ghur)
- **The ticks stay in the component.** They are not a parameter and not two-way: like the search
  text and the facets, they are Kelda parity state that goes away on refresh. What is worth
  sharing is the destination the selection produces, and that is a URL you own.
  (Fhi.Metadata-5ghur)
