category: Added

- **`FilterOptions` gains a `Datasamlinger` facet, matching the one the Explorer API now sends.**
  `GET /api/explorer/filters` answers with a `datasamlinger` list alongside the other facets, and
  the contract reads it as `DatasamlingFacet` — id, name, count, and both parents, so a host can
  place a datasamling in the kilde tree without a second request. `KildeId` is always present;
  `DelkildeId` is null for a datasamling hanging straight off its kilde, which most do. A host
  calling an API that predates the facet reads the list as empty and offers no datasamling filter.
  (Fhi.Metadata-ayxnn)
