category: Added
- The kilde detail view now loads an expandable hierarchy of delkilder, datasamlinger and
  variabelgrupper, initially collapsed. Descriptions and validity periods remain available
  in a separate disclosure. This is shared by the kilde explorer, the variable explorer's
  source view, and hosts mounting `KildeView` directly.
- Hosts can also render `KildeHierarchyView` with a `KildeId` and optional `Language`.
  `KildeView` now needs the registered `IMuninExplorerClient` to load the hierarchy.
- Hierarchy retries retain keyboard focus and announce completion. Rate-limited requests
  show the throttling message without enabling another retry; nested lists keep explicit
  list semantics when hosts hide their markers.
