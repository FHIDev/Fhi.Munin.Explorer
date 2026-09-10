category: Notes for hosts

- **The kildeutforsker's fold row wears `munin-explorer-filters__toolbar`, the name the
  variabelutforsker's own panel already uses**, and it is emitted as a direct child of
  `munin-explorer-filters`, which is what the rule pinning it to the top of a scrolling facet
  column selects on. A host that defines nothing gets the two buttons back in inline flow above the
  facets, which is a usable row and loses no information; what the rule buys is the row staying on
  screen once the reader has opened several facets and scrolled, plus the background that keeps the
  facets from showing through as they pass under it. `Fhi.Helsedata.Stiler` carries both, scoped to
  the width at which the panel is a scrolling sidebar — below that there is no scroll area to pin
  to and the row simply sits where it is drawn. Both sample stylesheets carry the flex row.
  (Fhi.Metadata-l9l2n.60)
