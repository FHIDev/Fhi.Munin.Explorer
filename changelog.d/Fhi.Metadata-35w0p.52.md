category: Changed
- **The saved-list view fills the chassis action row, and its summary says how many kilder the
  list draws from.** The list picker and the three download controls used to sit loose in the
  flow — this was the one detail surface that passed the chassis no `Actions` at all. Both now
  render in `munin-explorer-page__actions`, the row the other three detail views already fill,
  with the download behind a `<details>` so the two formats and the kodeverk tick do not stand
  across the row. The picker still appears only for a reader with more than one list. The line
  under the heading gains "fra N datakilder" / "from N sources", the distinct kilder in the whole
  list, written once the membership read has finished and left out until then.
  (Fhi.Metadata-35w0p.52)
