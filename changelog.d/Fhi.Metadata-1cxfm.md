category: Added

- **`IMuninExplorerClient` now carries the signed-in user's variable lists** - seven methods over
  `api/explorer/my/lists`: `GetMyListsAsync`, `CreateMyListAsync`, `RenameMyListAsync`,
  `DeleteMyListAsync`, `GetMyListVariablesAsync`, `AddVariablesToMyListAsync` and
  `RemoveVariablesFromMyListAsync`, with the `VariableList` and `VariableListItem` contracts they
  answer in. Ported from Runa's own client, so the routes, the verbs and the wire names are the
  ones the API already serves. (Fhi.Metadata-1cxfm)
- **These are the first calls in the package that need a token.** The whole of `my/lists` is behind
  the API's authenticated explorer policy, so a host registers its `IMuninExplorerTokenProvider`
  *before* `AddMuninExplorer` or every one of them answers 401 - which is thrown, not read as an
  empty list, because a host that believes it wired up sign-in has a fault rather than a user with
  nothing saved. The seam itself is unchanged: `BearerTokenHandler` attaches the token, and the
  anonymous default still wins when nothing is registered.
- **A batch of more than 2000 ids is refused before it is sent**, with a message naming the ceiling
  and what to do instead, rather than sent and answered with a `400` whose explanation
  `EnsureSuccessStatusCode` discards. `IMuninExplorerClient.MaxVariablesPerBatch` is that ceiling,
  and splitting is left to the caller on purpose: a client-side split turns one call that either
  happened or did not into several that may have half happened, with nothing in the return value to
  say which.
- **A list that is not the caller's answers `false`, or `null` for the paged read.** The API cannot
  distinguish a list deleted in another tab from somebody else's and deliberately does not try -
  both are `404`, so that a caller cannot probe for which list ids exist. That is the same
  not-a-fault the read endpoints already map to `null`.
