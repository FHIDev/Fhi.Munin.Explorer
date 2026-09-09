category: Added

- **`HierarchyVariabelgruppe` gains `PresentationOrder`.** The hierarchy endpoint sends
  `presentationOrder` on a variabelgruppe as it does on the delkilder and datasamlinger above it,
  and the contract had nowhere to put it, so a host reading the tree could not see the curated
  order at all. `int?`, null when unordered, the same shape the two neighbouring records already
  use. (Fhi.Metadata-l9l2n.61)
