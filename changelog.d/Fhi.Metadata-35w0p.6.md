category: Changed
- **The kilde, datasamling and variable detail views read downwards in one column.** The sidebar
  each of them kept beside the main column is gone, and every block that lived in it now sits in
  the main flow: Kildeinformasjon and Statistikk on a kilde and a datasamling, and
  Kildeinformasjon, Dataperiode, Datatype, Variabelgrupper and Datasamlinger on a variable. Nothing
  was dropped, and the nine section ids a deep link uses are unchanged and still in the order they
  were. Two things about the order are worth knowing. The moved blocks keep the position they were
  already read in relative to the view's own blocks, so on a page with nothing passed into the
  `Sections` slot the reading order is exactly what it was. Where an explorer or a host does pass
  sections — Kelda's Variabler, Kriterier for tilgang til data and Priser — they now come *after*
  the moved blocks rather than before them, because the slot stays last: a host's own sections
  are additions to the page the component is. That reordering applies at every width. The layout
  change is desktop-only, since below 1024px the two columns already stacked.
  (Fhi.Metadata-35w0p.6)
