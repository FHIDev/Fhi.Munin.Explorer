category: Fixed
- **A datatype no longer reads "String" in a result row beside "Streng" on the variable's detail
  panel and on the datatype facet.** The list rows in the search results and in the reader's saved
  list name a datatype from the API's own facets, and the filters endpoint echoes back the word a
  variable predating the codes was stored as — `displayName` "String" for code `1`, whatever
  language the call asked for. A stored value is now resolved to its code before the facet is
  looked up, so a row holding "String" finds the same facet a row holding "1" does, and both list
  paths and the facet itself then pass the API's word through the same alias table: a legacy
  stored spelling, English or Norwegian, becomes the shipped table's name for the code it means,
  in the reader's own language — "Streng" under `no`, "String" under `en`. Every other name
  reaches the page exactly as the API sent it, so a datatype added on the API's side is named by
  the API rather than by a table frozen inside this package. When no API name reaches a row at all
  — the filters call failed, or has not answered yet, or answered without a name for that code —
  the row now falls back where the facet and the detail panel already did, to the shipped word for
  the code, rather than showing the bare number beside a facet showing a word. A datatype the
  shipped table has never heard of still reads as its code there, as it does on the panel.
  (Fhi.Metadata-l9l2n.49)
