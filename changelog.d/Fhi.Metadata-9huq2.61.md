category: Added
- **A link can open one of the signed-in reader's own variable lists.** `?listeId=<list id>` on the page
  `VariableExplorer` is mounted on opens the «Variabelliste» tab on that list, for a page such as an application form
  that lists a reader's lists and links back to each. It opens the live list, notes included, where `?delekode=` opens
  a snapshot copy. A list that is not the reader's says it was not found; a signed-out reader is asked to sign in, and
  the link opens the list once they have. Behind `MuninExplorer:Features:ListLink`, off by default.
  `ExplorerUrlState.OwnListId`, `VariableSearch.OwnListId` and `VariableListView.OwnListId`/`OwnListIdChanged` carry
  it for a host that lays the surfaces out itself.
