category: Added
- The rest of the Explorer API is now on `IMuninExplorerClient`: `GetFiltersAsync`,
  `GetKilderAsync`, `GetKildeAsync`, `GetKildeHierarchyAsync`, `GetDatasamlingAsync`,
  `GetVariableAsync` and `GetVariableTimelineAsync`, with contracts to match. A resource that
  does not exist answers `null`, or an empty collection, instead of throwing.
- `VariableSummary` gained `PresentationOrder`, `DataType` and `VersionId` — the API was
  already returning all three.
