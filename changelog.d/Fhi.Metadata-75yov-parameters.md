category: Changed
- **`KildeSearch` takes `@bind-MarkedDatasamlinger` and `ExploreDatasamlingerRequested`.**
  `ExploreVariablesRequested` keeps its signature and still carries kilde ids only, so a host
  composing `KildeSearch` itself is untouched. The datasamling column is drawn only where both
  callbacks are wired — a mark has nowhere to go without the second — and so is the whole of the
  mark half: without `ExploreDatasamlingerRequested` a `MarkedDatasamlinger` a host passes in seeds
  nothing, counts in no bar and opens no row, rather than building towards a handover that would be
  refused. At most twenty marked rows open themselves on the first render whatever the address
  holds, and only for kilder the list has and says hold a datasamling: each one is a catalogue
  request charged to the rate-limit window helsedata's cluster shares, and the query is untrusted.
  The marks past that bound are still held, still counted and still travel.
  `KildeExplorer.MarkedQueryKey` names the query key it mounts them from.
  (Fhi.Metadata-75yov)
