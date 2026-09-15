category: Fixed
- **A variable name or column header cut off in the result list now shows its full text on
  hover.** Stiler clips both to one line, and every non-empty data value already carried its full
  text as a `title`, but the name and the headers did not. Measured on real data, 17 to 19 names
  per page were clipped at every desktop width, and the Variabelgruppe header at 1300px. The name
  span and a new unclassed `<span>` around each header's label now carry a `title`. An empty name
  gets none. The title sits on the span and not on the `columnheader` or its sort button, where
  Edge exposes it on the sorted column as a description repeating the name. Accessible names are
  unchanged, and no class name is added. (Fhi.Metadata-1vm16)
