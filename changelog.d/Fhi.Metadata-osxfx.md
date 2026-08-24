category: Changed
- **The public API is now English throughout** - the package started out following Munin's own
  Norwegian identifiers, and this renames the lot before the first publish to the feed, while it
  still costs nothing. The component is `VariableExplorer` with `Search`, `SearchChanged`,
  `PageSize` and `Language` parameters; `IMuninExplorerClient` answers `SearchVariablesAsync`,
  `GetFiltersAsync`, `GetKilderAsync`, `GetKildeAsync`, `GetKildeHierarchyAsync`,
  `GetDatasamlingAsync`, `GetVariableAsync` and `GetVariableTimelineAsync`;
  `IMuninExplorerTokenProvider` answers `GetTokenAsync`; and the contracts are `Page<T>`,
  `VariableSummary`, `VariableDetail`, `VariableVersion`, `KildeSummary`, `KildeDetail`,
  `KildeHierarchy`, `DatasamlingDetail`, `FilterOptions`, `PropertyMetadataEntry` and the `*Facet`
  records. DTO properties follow — `Navn` is `Name`, `Beskrivelse` is `Description`,
  `GyldigFra`/`GyldigTil` are `ValidFrom`/`ValidTo`, `Dataansvarlig`/`Databehandler` are
  `DataController`/`DataProcessor`, and so on. **The JSON contract is unchanged**: every
  property carries an explicit `[JsonPropertyName]`, so the wire still spells everything
  Munin's way. Domain terms with no honest translation stay Norwegian inside otherwise-English
  names — `KildeId`, `DatasamlingCount`, `GetKildeHierarchyAsync` — and so do their Norwegian
  plurals, because those are the API's own field names. `AGENTS.md` records where the line sits
  and why. (Fhi.Metadata-osxfx)
- **The root element's class is now `variable-explorer`** - it carries no styling in Stiler or
  in this package and exists only so the component can be found in the DOM of a CMS page. A
  host with its own selector for the old `variabelutforsker` has to update it. User-facing
  Norwegian is untouched: every label, status message and error string reads exactly as before.
  (Fhi.Metadata-osxfx)
