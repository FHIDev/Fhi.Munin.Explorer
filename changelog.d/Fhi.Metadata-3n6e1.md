category: Changed
- **The variabelutforsker takes its kildetype words from the API rather than from a table shipped
  inside the package.** `GET /api/explorer/filters` resolves `kildeTyper[].displayName` from the
  Kilde-scoped Kildetype master data and follows `Accept-Language`, so an edit there now reaches
  the page. The shipped table stays as the fallback for an API that answers with the raw enum name
  or with no `displayName` at all. Checked against the test API on 2026-09-10 the two agree word
  for word on all eight values in both languages, so no visible text changes today.
- **The kilde facet's kildetype headings say what the facet button above them says.** A kildetype
  Munin adds that this package has no word for used to read as prose on the button and as its bare
  token — `nyKildetype` — on the heading directly beneath it, in the same panel. Both now take the
  API's word, as does the kilde trail in an opened result row. (Fhi.Metadata-3n6e1)
