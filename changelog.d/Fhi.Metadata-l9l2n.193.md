category: Fixed
- **The IntelliSense docs for the saved-list calls say what the client does.** A missing token is a
  `MuninExplorerUnauthorizedException`, not an `HttpRequestException`; `SetMyListDesiredDataAsync`
  is one of several writes answering a `DesiredDataResult`, not the only one; and both
  `ShareListAsync` and `ShareMyListAsync` share the chosen codes (`DesiredDataCodes`) but never the
  free text or the notes. No behaviour changed. (Fhi.Metadata-l9l2n.193)
