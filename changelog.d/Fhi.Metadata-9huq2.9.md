category: Added
- **A variable can be shared from the search results and from «Vis hele variabelen», signed in or not.** In
  `VariableExplorer`, an open search row and the whole-variable view now carry «Del variabel» with «Kopier lenke» and
  «Send via e-post», as a row in the reader's own variable list already did. The link opens that one variable: the
  search for its code within its kilde, with the row open. The e-mail's subject is «Variabel: ‹navn›» and its body
  carries the link. A host that declines the `search` or `variabelId` address keys gets no share group, as before in
  the list.
- **`VariableSearch` takes a `VariableHref` parameter for that link.** `VariableExplorer` passes it; a host mounting
  `VariableSearch` alone and leaving it null gets no share group, as before. The share group itself is the
  `VariableShare` component, public only because Razor makes every component so, and not meant to be mounted alone.
