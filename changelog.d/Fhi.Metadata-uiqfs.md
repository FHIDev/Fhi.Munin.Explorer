category: Added

- **`VariableList` gained `VariableCount`, and the saved-list picker now says what every list
  holds** rather than only the one on screen. Munin's `GET api/explorer/my/lists` started sending
  `variableCount` and nothing read it; the picker's options read `Mine hjertevariabler (247
  variabler)` now, and a list with nothing in it reads `0 variabler`. The line under the picker
  takes its count from the same field, so the two cannot disagree about the same list.
  (Fhi.Metadata-uiqfs)
