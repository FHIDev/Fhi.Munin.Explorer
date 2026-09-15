category: Added
- **`KildeHierarchy` carries how many of the kilde's variables have expired.**
  `GET /api/explorer/kilder/{id}/hierarchy` sends `historicalVariableCount` beside
  `totalVariableCount`, and the contract now reads it as `HistoricalVariableCount`. The two are
  disjoint: `TotalVariableCount` is what a reader can open today, `HistoricalVariableCount` the
  variables whose every published version has expired. It reads as 0 against an API that predates
  the field. (Fhi.Metadata-l9l2n.113)
