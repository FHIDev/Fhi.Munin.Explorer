category: Added
- **`DatasamlingFacet` carries the datasamling's datakategorier.** `GET /api/explorer/filters` sends
  `categories` on every datasamling now, so a host drawing datakategori glyphs beside a filter row
  reads them off the facet it already has instead of one hierarchy request per kilde. The tokens are
  the ones `HierarchyDatasamling.Categories` carries — EHDS CURIEs such as `ehds-cat:biobanks` and
  bare codes such as `RPDG` both occur, so match whole tokens rather than a prefix — and the list is
  empty rather than null against an API that predates the field. (Fhi.Metadata-bajdj)
