category: Added
- **Two fields the Explorer API had already started sending** - `FilterOptions.DataCategories`
  (`datakategorier`), the EHDS datakategori facet with its counts, and `PropertyMetadataEntry.Options`
  (`options`), the allowed values of a `SingleSelect` or `MultiSelect` property already parsed and
  already resolved to the request's language. A host rendering those values no longer has to parse
  `OptionsJson` itself, which is what this package used to tell it to do. Both were found by the new
  nightly contract check on its first run against the live API — the API and this package release
  separately, so nothing here had noticed either one. (Fhi.Metadata-l9l2n.20)
