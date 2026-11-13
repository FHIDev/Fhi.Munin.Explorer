category: Added
- **The variable search gains avansert søk, behind `MuninExplorer:Features:AdvancedSearch`.** An «Avansert søk»
  button under the search field opens a form of rows, each a field, an operator and a value: text, a date or a
  range, or ticked codes, with the operators the field's type takes. «Legg til betingelse» adds a row (up to ten),
  «Nullstill» starts over and «Vis treff» applies them. Every condition must hold, together with the facets. Each
  condition then shows as a chip beside the facet chips, removable on its own, with «Endre avansert søk» to reopen
  the form; «Fjern alle filtre» clears conditions and facets together. The button counts the conditions in force:
  «Avansert søk (3)».
- **`VariableFilter.Conditions` carries the conditions, as the API's repeated `vilkar=felt:operator:verdi`.** So the
  list, the facet counts, an export and the URL a host writes from `FilterChanged` all carry them, and a shared link
  restores the search. `SearchCondition` is one condition, `ConditionField` one field from the new
  `IMuninExplorerClient.GetConditionFieldsAsync` (`GET api/explorer/variables/vilkar`), whose default answers empty.
  A condition a link carries that the API would refuse is dropped before the first search, and a repeated one is
  kept once; when the fields cannot be loaded, the link's conditions are kept unjudged, a repeated one still once.
