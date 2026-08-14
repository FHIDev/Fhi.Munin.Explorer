category: Added
- The rest of the Explorer API is now on `IMuninExplorerClient`: `HentFiltreAsync`,
  `HentKilderAsync`, `HentKildeAsync`, `HentKildeHierarkiAsync`, `HentDatasamlingAsync`,
  `HentVariabelAsync` and `HentVariabelTidslinjeAsync`, with contracts to match. A resource that
  does not exist answers `null`, or an empty collection, instead of throwing.
- `VariabelSammendrag` gained `PresentationOrder`, `DataType` and `VersjonId` — the API was
  already returning all three.
