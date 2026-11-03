category: Deprecated
- **`KildeSearch.ExploreDatasamlingerRequested` is obsolete and will be removed in 3.0.0.** Wire
  `ExploreSelectionRequested` instead. A host still wiring only the old one keeps the old behaviour:
  every ticked kilde is fetched and expanded to all its datasamlinger, and the press is now refused
  when that list is too long for the address. Setting the parameter in markup gives warning
  CS0618, which fails the build under `TreatWarningsAsErrors`. (Fhi.Metadata-9huq2.13)
