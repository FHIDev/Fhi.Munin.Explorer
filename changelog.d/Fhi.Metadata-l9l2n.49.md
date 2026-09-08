category: Fixed
- **A datatype no longer reads "String" in a result row beside "Streng" on the variable's detail
  panel and on the datatype facet.** The list rows in the search results and in the reader's saved
  list name a datatype from the API's own facets, and the filters endpoint answers a Norwegian
  call with `displayName` "String" for code `1`. Both list paths now pass the API's name through
  the same legacy-alias table the panel already used, and the facet reads its own `displayName`
  the same way rather than resolving the shipped table by the code — so a datatype the API adds on
  its side is named by the API in the rows and on the facet alike, instead of the facet showing a
  bare code. The API stays authoritative: only a known legacy English form is replaced, and any
  other name renders exactly as it arrived. The detail panel cannot follow it that far, because
  the variable carries the code and no name, so a datatype the shipped table has never heard of
  still reads there as its code. (Fhi.Metadata-l9l2n.49)
