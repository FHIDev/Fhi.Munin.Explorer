category: Added
- **The kilde list can be sorted, and the order is in the link.** `KildeSearch` draws a
  "Sorter etter" select above the table offering Navn A–Å, Flest variabler, Sist endret and
  Opprettet beside the order the catalogue sent, and `KildeExplorer` reads and writes that order as
  `?sort=` next to `?kilde=` — omitted while the list is in the order it arrived in, so links made
  before this release still mean what they did. The sorting is done in the browser over the rows
  the search and the facets left: `GetKilderAsync` is not paged, so the whole catalogue is already
  in hand and nothing here is asked of the API. Names are collated as `nb-NO`, pinned, whatever
  language the reader has asked for, so æ, ø, å and the digraph aa all come at the end of the
  alphabet — a kilde spelled Aa sorts beside Å rather than beside A. A kilde with no value to order
  by sorts last in every order, never among the smallest, while a recorded zero is ordered as
  zero. A host owning its own query string mounts `KildeSearch` and binds the new
  `Order` / `OrderChanged` pair. (Fhi.Metadata-lhdh0)
