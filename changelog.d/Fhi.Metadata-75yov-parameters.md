category: Changed
- **`KildeSearch` takes `@bind-MarkedDatasamlinger` and `ExploreDatasamlingerRequested`.**
  `ExploreVariablesRequested` keeps its signature and still carries kilde ids only, so a host
  composing `KildeSearch` itself is untouched. The datasamling column is drawn only where both
  callbacks are wired — a mark has nowhere to go without the second — and
  `KildeExplorer.MarkedQueryKey` names the query key it mounts them from.
  (Fhi.Metadata-75yov)
