category: Changed
- **The kildeutforsker's facets fold, one open at a time.** Every facet used to render open at
  once — the databehandler facet alone has 39 values on the live catalogue — which left the
  filter column longer than the list it filters. Each facet is now a native `<details>`: the
  first starts open so the affordance is visible, the rest start folded, and a folded facet's
  header carries the number of values ticked inside it (`Kildetype (2)`), so a filter cannot
  narrow the list from behind a closed disclosure without saying so. The same shape and the
  same count the variable explorer's own panel already uses, under the same
  `munin-explorer-filters` handle, so no new class name and nothing new for a host to style.
  A facet's values are rendered only while it is open rather than drawn and hidden.
  (Fhi.Metadata-co3sf)
