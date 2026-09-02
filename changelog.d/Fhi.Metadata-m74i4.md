category: Added

- **The saved-list view has an "Ønskede data" column, and a signed-in reader can write in it.**
  Free text per variable — what the reader wants out of that variable, which is the other half of
  a data application from which variables they picked. It is stored server side, so the same note
  written in Runa shows up here and the other way round; the two surfaces stopped disagreeing about
  the same list. Signed out the component still renders nothing at all, exactly as before: it has
  no anonymous list, and this change does not add one. (Fhi.Metadata-m74i4)
- **`IMuninExplorerClient` gained `SetMyListDesiredDataAsync`, and `VariableListItem` gained
  `DesiredDataType` and `DesiredDataFreeText`.** The two fields are optional and additive, so a
  host reading the rest of the item is unaffected. The method carries a default body that throws,
  like `ExportListAsync` before it, so a host implementing the interface itself keeps building.
- **The API's refusal of an over-long note reaches the reader.** The text is capped at 500
  characters server side, and the cap is not written down in this package: `DesiredDataResult`
  carries the ceiling the API named, so the sentence the reader sees quotes the API's own number
  and cannot drift from it. Their text stays in the field rather than being reverted under them.
- **A refused note stays refused until it is rewritten.** The mark on the field and the sentence
  naming the ceiling used to be dropped by the next thing the reader did — saving another row,
  removing one, downloading the list — leaving 500-odd unsaved characters looking saved, or the
  field marked wrong with nothing saying why. Both now stand until that row is written again or
  leaves the list, in an alert region of their own that the field points at, and the text survives
  a reload from anywhere.
