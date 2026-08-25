category: Added

- **Kelda's kilde list has facets** - `KildeExplorer` now draws a filter panel over kildetype,
  kategori, tilgangsnivå and databehandler, with a checkbox per value and a count beside it.
  Ticking narrows the list client-side: OR within a facet, AND across them, and AND with the
  search. Everything is computed over the one list the component already fetched, so no facet
  costs a request and none of them is a server-side filter — including kildetype, which the
  endpoint would take, because two facets behaving differently is a difference a reader can feel
  and nobody can explain. The counts are therefore not cross-filtered: an option's number is how
  many kilder in the catalogue carry that value, not how many the current selection would leave.
  (Fhi.Metadata-2fomm.3)
- **A facet with no values is not drawn at all** - no heading, no empty container. Munin's own
  Kelda renders Kategori as a heading with nothing under it, which reads as a broken panel rather
  than as a field nobody has filled in; leaving the facet out makes "is the data there?" a question
  about the catalogue, which this component then answers correctly either way. (Fhi.Metadata-2fomm.3)
- **Kategori's choices read as words rather than as EHDS tokens** - the catalogue stores a kilde's
  kategori as a CURIE — `ehds-cat:registries-quality-of-healthcare` — and the list endpoint sends
  no vocabulary beside it, so the panel labels them from a copy of the vocabulary the kilde detail
  endpoint carries: "Kvalitetsregistre", in both languages the catalogue writes it in. The same
  treatment tilgangsnivå gets, and for the same reason — one panel cannot be in two minds about
  whether a reader of this catalogue is expected to read EHDS. Being a copy, it can fall behind:
  a token the catalogue adds later keeps its checkbox and its count and shows its CURIE, which is
  unlovely and still filterable. The facet groups and filters on the whole token throughout, so
  what a choice is called never changes what it selects. One category is one checkbox however the
  catalogue wrote it — an array, a bare JSON string, or text that is not JSON at all — and a JSON
  null is no category rather than a checkbox named "null". (Fhi.Metadata-2fomm.3)
- **A choice drawn in the catalogue's own Norwegian is marked as being in it** - databehandler is
  free text, and kildetype falls back to Munin's own token wherever this package has no word for
  it. Those choices carry `lang`, exactly as the same strings do in the table's cells, so an
  English page does not have a Norwegian organisation's name read out with English phonetics
  (WCAG 3.1.2). A choice this package supplied the words for carries none, because a `lang` the
  text is not in is the same failure the other way round — and so does a kategori or tilgangsnivå
  the vocabularies had no word for, because what is left on screen there is an EHDS or EU CURIE,
  English-authored and prose in no language at all. (Fhi.Metadata-2fomm.3)
- **A long free-text facet value no longer decides the layout** - databehandler is free text, and
  one value on the live catalogue runs to 212 characters. The choice is cut to 60 characters on
  screen with the whole value on its `title`, and the value it filters on is untouched. Variants
  are not merged: "FHI" and "Folkehelseinstituttet" stay two choices, because deciding they are one
  organisation is a claim about the catalogue and belongs in it (`Fhi.Metadata-4kxfv`).
  (Fhi.Metadata-2fomm.3)
- **The panel folds away on a narrow screen** - a "Vis filtre" button unfolds it, using the
  browser's own `hidden` attribute so it works on a host that styles none of this; a host with room
  for a sidebar takes the folding away in one rule, which is what both sample stylesheets now do.
  Two class names are new, `munin-explorer-filters__toggle` and `munin-explorer-filters__facets`,
  and a host on `Fhi.Helsedata.Stiler` needs rules for them. (Fhi.Metadata-2fomm.3)
- **The kilde list's empty state names the facets as well as the search** - "Ingen kilder samsvarer
  med søket «als» og filtrene som er valgt". A reader who has narrowed the list twice was being sent
  to fix the wrong one. (Fhi.Metadata-2fomm.3)
