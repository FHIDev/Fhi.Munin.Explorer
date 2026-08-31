category: Fixed

- **The datakategori and dataperiode filters are drawn.** Both were carried by the contract and by
  shareable links already, and neither was ever rendered — so someone using the explorer through
  helsedata.no could not filter on them while the same person could in Runa. Datakategori is an
  ordinary multi-select facet; dataperiode is a from and a to date, bounded by the range the API
  reports for the current selection. (Fhi.Metadata-uidue)
- **Datakategori shows the catalogue's own words**, resolved through the same property-metadata
  vocabulary Kelda reads, so the panel says "Befolkningsundersøkelser" rather than
  `ehds-cat:population-health-surveys`. A token the vocabulary does not name is shown as itself
  rather than dropped. Losing the vocabulary costs the choices their words and nothing else — the
  facet still filters, and reports no second error.
- **Placement is Runa's where it can be.** Dataperiode takes Runa's own slot, after Datatype and
  before Helsefaglig kodeverk. Datakategori is third rather than Runa's first, because the two
  above it are in helsedata's own order deliberately.
- A facet may now carry **its own control** instead of a list of values or an empty-state sentence.
  The dataperiode needed it: holding no facet values, it was dropped as empty under the old rule,
  and given empty text to survive that it drew the sentence instead of the date fields. No new CSS
  class name — the date fields are native inputs, for the reason the panel's `<details>` and bare
  `<ul>` are elements too.
