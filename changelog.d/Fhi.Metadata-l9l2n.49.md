category: Fixed
- **A datatype now reads the same word in a result row as it does on the variable's detail
  panel.** The list rows in the search results and in the reader's saved list name a datatype from
  the API's own facets, and the filters endpoint answers a Norwegian call with `displayName`
  "String" for code `1` — so a row read "String" beside a detail panel and a datatype facet both
  reading "Streng". Both list paths now pass the API's name through the same legacy-alias table the
  panel and the facet already used, so all four surfaces say one word for one value; the panel and
  the facet were already right and are unchanged. The API stays authoritative: only a known legacy
  English form is replaced, and any other name — a datatype added on the API's side, a code with no
  name yet — renders exactly as it arrived. (Fhi.Metadata-l9l2n.49)
