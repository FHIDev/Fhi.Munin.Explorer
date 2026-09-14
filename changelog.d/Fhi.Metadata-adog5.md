category: Changed
- **Every facet that nests values is now a tree the reader opens branch by branch, and the Kilde
  facet's datasamlinger are in it.** Any value with something under it — a kildetype group, a kilde,
  a delkilde, and equally a variabelgruppe nested under another or a saved filter under another —
  now carries a disclosure control of its own beside the checkbox, and every one of them starts
  shut. Opening a branch narrows nothing, ticks nothing and asks the API for nothing: the whole tree
  comes from the `GET /api/explorer/filters` answer the panel already had, so a reader can look
  inside a kilde without filtering on it. The datasamling level is drawn from
  `FilterOptions.Datasamlinger`, each row placed under the delkilde its `DelkildeId` names or
  directly under its kilde where that is null — which is the majority of them — and the counts are
  the cross-filtered ones the same answer carries. The values under a shut branch are not rendered
  at all rather than hidden, so nothing inside one can be tabbed into; a value ticked inside a
  branch stays ticked while it is shut, keeps its chip over the results and keeps its place in the
  facet's own count. Utvid alle and Skjul alle reach the branches as well as the facets, and a term
  typed into the Kilde facet's own search opens the branches down to what it matched — that search
  matches the names of delkilder and datasamlinger too, and a match nobody can see would leave a
  kilde on screen for no visible reason. (Fhi.Metadata-adog5)
