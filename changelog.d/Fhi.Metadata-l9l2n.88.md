category: Changed
- **The kildeutforsker sorts by pressing a column heading, and the "Sorter etter" select is gone.**
  Navn, Variabler, Sist endret and Opprettet each hold a `<button>` now; pressing one sorts on that
  column ascending and pressing it again reverses it, which is exactly what the variabelutforsker
  has always done. The select over the table has been removed rather than kept in step — two
  controls for one piece of state is what this change exists to avoid — so the catalogue's own
  order is what a first load and a clean URL give and is no longer re-selectable in the page. That
  is intended: it is the absence of a sort rather than one of the five, and it was already the one
  order never written to a host's URL. Hosts setting `KildeSearch.Order` should know that a member
  now names a column rather than a direction: `Variables` used to mean most-first, and means
  fewest-first unless the new `Direction` parameter says otherwise. `KildeExplorer` carries the new
  half in `?sortDir=`, omitted while the column runs ascending, and `Sist endret` is reachable only
  once its column is turned on in the picker. (Fhi.Metadata-l9l2n.88)
