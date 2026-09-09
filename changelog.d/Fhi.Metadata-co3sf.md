category: Changed
- **The kildeutforsker's facets fold.** Every facet used to render open at once — the databehandler
  facet alone has 39 values on the live catalogue — which left the filter column longer than the
  list it filters. Each facet is now a native `<details>`: the first starts open so the affordance
  is visible, the rest start folded, and each folds independently from there. A folded facet's
  summary carries the number of values ticked inside it, so a filter cannot narrow the list from
  behind a closed disclosure without saying so. Opening a kilde and coming back leaves the search
  and the ticked values as they were and the folds back at their defaults, because the fold belongs
  to the browser and the panel is rebuilt. The same shape the variable explorer's own panel already
  uses, under the same `munin-explorer-filters` handle, so the fold itself asks nothing new of a
  host. (Fhi.Metadata-co3sf)
