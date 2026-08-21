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
  otherwise English page. (Fhi.Metadata-l9l2n.16)
