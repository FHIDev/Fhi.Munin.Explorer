category: Added
- **Saved variable lists show two coverage columns, Kodeverk and Statistikk** - `VariableListView`
  draws them after Dataperiode on every list, with no picker, since this table has none. Each cell
  says whether the variable links a kodeverk or has published statistics, as the words "Ja"/"Nei"
  ("Yes"/"No" in English), never as a glyph. The headings are the catalogue's own terms, Kodeverk
  and Statistikk, in both languages. The values come from `hasKodeverk` and `hasStatistikk`, which
  the `my/lists/{id}/variables` endpoint already sends, so no extra request is made.
  `VariableListItem` gains `HasKodeverk` and `HasStatistikk` as `bool?`. Null means not known: the
  API sends null for an entry whose variable has left the catalogue, and a shared list's snapshot
  does not carry the flags. Those cells read "Ikke oppgitt" ("Not specified"), like every other
  cell with no value, rather than "Nei". A shared list shows the same two columns.
  (Fhi.Metadata-l9l2n.95)
