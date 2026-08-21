category: Fixed

- **A `Language` carrying a region now resolves to its language rather than falling back to
  Norwegian** - `en-GB` and `en-US` read as English, `nb-NO` as Norwegian, and the match is on the
  primary subtag throughout. helsedata's CMS reports the short branch name (`no` / `en`), but the
  same solution builds full cultures elsewhere, and an exact match on `en` handed an English page
  Norwegian labels, dates and filter names with nothing thrown and no test failing.
  (Fhi.Metadata-l9l2n.16)
- **The filter panel asks the API for the language the rest of the component is rendering in**,
  rather than passing the host's raw token through as `Accept-Language`. The datatype facet's names
  are resolved server side, so a token the API did not recognise left that one block Norwegian on an
  otherwise English page. The header carries the API's own spelling of Norwegian, `nb`, rather than
  helsedata's `no`: `no` has no parent culture the API's request localization can fall back from,
  so it would quietly resolve to the API's default language instead. (Fhi.Metadata-l9l2n.16)
- **A host built with `InvariantGlobalization` no longer takes the property rows down.** Dates and
  the catalogue's sort order fall back to the invariant culture where `nb-NO` is unavailable,
  rather than throwing mid-render — and, for the sort order, throwing once from a static
  initialiser that cannot be retried. Both cultures resolve once at type load rather than per call,
  so such a host does not construct and catch an exception for every date it draws.
  (Fhi.Metadata-l9l2n.16)
