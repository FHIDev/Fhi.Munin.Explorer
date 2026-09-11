category: Added
- **`KildeSearch.InitialDirection(KildeSortOrder)` is a new public method, and a supported entry
  point rather than an accident of the explorer's URL code.** It answers which way a column runs
  before anybody presses it: `Ascending` for `Name` and for `Standard`, `Descending` for
  `Variables`, `SourceUpdated` and `Established`. A host needs it because `KildeSearch.Direction`
  is a `SortDirection`, a struct with no unset state — it is `Ascending` whatever the order is, so
  a host setting `Order` from a `?sort=` link and nothing else silently opens a count or date
  column at its smallest value rather than at the end a reader of that link saw. Passing
  `InitialDirection(order)` alongside `Order` is what reproduces a heading's first press, and is
  what `KildeExplorer` does. Being public is a commitment: these per-column defaults are now part
  of the package's surface, and changing one is a breaking change for hosts, not only for the
  headings here. (Fhi.Metadata-l9l2n.88)
