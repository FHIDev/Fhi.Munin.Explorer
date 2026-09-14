category: Changed
- **The Kilde facet is a tree the reader opens branch by branch, and its datasamlinger are in
  it.** Every branch with something under it — a kildetype group, a kilde, a delkilde — now carries
  a disclosure control of its own beside the checkbox, and every one of them starts shut. Opening a
  branch narrows nothing, ticks nothing and asks the API for nothing: the whole tree comes from the
  `GET /api/explorer/filters` answer the panel already had, so a reader can look inside a kilde
  without filtering on it. The datasamling level is drawn from `FilterOptions.Datasamlinger`,
  each row placed under the delkilde its `DelkildeId` names or directly under its kilde where that
  is null — which is the majority of them — and the counts are the cross-filtered ones the same
  answer carries. The values under a shut branch are not rendered at all rather than hidden, so
  nothing inside one can be tabbed into; a value ticked inside a branch stays ticked while it is
  shut, keeps its chip over the results and keeps its place in the facet's own count. Utvid alle
  and Skjul alle reach the branches as well as the facets. (Fhi.Metadata-adog5)
