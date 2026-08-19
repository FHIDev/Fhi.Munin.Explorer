category: Changed
- **`IMuninExplorerClient` takes a `VariableFilter`** - on `SearchVariablesAsync`, which gains it as
  a second parameter, and on `GetFiltersAsync`, where it replaces the `kildeType` parameter with the
  whole selection. Both are breaking: existing calls that pass positional arguments after the search
  term stop compiling, and a caller passing `kildeType` must wrap it as
  `new VariableFilter { KildeType = ... }`. The filter covers everything the API filters on,
  including datasamling and EHDS datakategori, which the filters endpoint reports no facet for and
  the panel therefore does not draw. A filter that narrows nothing adds nothing to the URL, so an
  unfiltered search is byte-identical to what it was before. (Fhi.Metadata-l9l2n.13)
