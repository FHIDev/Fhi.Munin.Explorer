category: Changed
- **A datasamling page now draws the sections Munin's placement rows declare, under their names and
  in their order, instead of one fixed "Metadata" block and three of the view's own.** Where the
  catalogue has placed a datasamling's properties, each of its sections is a section of the page —
  "Om datasamlingen", "Variabler", "Datakilde", "Alle metadatafelt" and whatever else the placement
  rows carry — so "Kildeinformasjon" is renamed to "Datakilde" and Kvalitetsnote appears without
  this package being changed again. The fact rows the placement did not take follow the fields it
  did into the same section rather than heading a second one about the same subject, so the source
  block's parent, kildetype and Munin timestamps sit under "Datakilde" and the variable count under
  "Variabler"; nothing is dropped. A payload whose properties carry no `groupKey` and
  `groupSortOrder` — an API predating the placement rows — renders exactly as before: one
  "Metadata" block, then the inclusion and exclusion criteria, "Kildeinformasjon" and "Statistikk".
  (Fhi.Metadata-lr6yh)
