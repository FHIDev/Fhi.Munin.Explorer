category: Added
- **`FilterOptions` carries the whole variabelgruppe hierarchy and each group's visibility.**
  `GET /api/explorer/filters` answers with a second variabelgruppe collection,
  `hierarkiVariabelgrupper`, read as `FilterOptions.HierarchyVariabelgrupper`: every group the
  current selection reaches, with the kilde, delkilde and datasamling it hangs under in
  `VariabelgruppeFacet.Owners`. A host can draw a folder tree from the answer the facets already
  come in, instead of one `kilder/{id}/hierarchy` call per source a reader expands.
  `VariabelgruppeFacet` also gains the stored `Filter` value and the admin-curated `Global` flag,
  so the two surfaces can be told apart: the tree shows every group, and the standalone facet
  withholds the ones marked `"2"` — which arrive nonetheless, because the tree needs them, and
  because an opted-out group still appears in `Variabelgrupper` as the trunk an offered descendant
  nests under. `IsStandaloneFacetOption` is that test; which collection a row came from is not.
  `Filter` is a `string?` rather than a bool, so a group whose source file left it unset stays
  distinguishable from one that opted out. `HierarchyVariabelgrupper` is empty against an API
  predating the change, where `Variabelgrupper` keeps reading as before — with `Filter` null,
  `Global` false and `Owners` empty, since that API sends none of the three.
