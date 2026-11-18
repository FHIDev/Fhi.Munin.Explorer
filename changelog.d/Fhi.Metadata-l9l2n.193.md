category: Fixed
- **The IntelliSense docs for the saved-list calls say what the client does.** `IMuninExplorerClient`'s
  remarks scope the token requirement to the `my/lists` methods (the export and share calls beside
  them are anonymous) and name the null and `DesiredDataOutcome.NotFound` answers beside `false`;
  `SetMyListDesiredDataAsync` is one of five writes answering a `DesiredDataResult`, not the only one,
  and `DesiredDataResult` names all five; and both `ShareListAsync` and `ShareMyListAsync` share the
  chosen codes (`DesiredDataCodes`) but never the free text or the notes. No behaviour changed.
  (Fhi.Metadata-l9l2n.193)
