category: Changed
- **The kildeutforsker sorts by pressing a column heading, and the "Sorter etter" select is gone.**
  Navn, Variabler, Sist endret and Opprettet each hold a `<button>` now; pressing one sorts on that
  column and pressing it again reverses it, the two-state toggle the variabelutforsker has always
  had. Where the two deliberately differ is the *first* press: there every column starts ascending,
  while here a count or a date column starts at its largest value and only Navn starts at A. That
  is what each column's old select label said it meant — "Flest variabler", "Sist endret (nyest
  først)", "Opprettet (nyest først)" — so a `?sort=Variables` link made against 0.1.0-alpha.11
  still opens the end of the list it opened then, rather than quietly the other one. The select
  over the table has been removed rather than kept in step — two
  controls for one piece of state is what this change exists to avoid — so the catalogue's own
  order is what a first load and a clean URL give and is no longer re-selectable in the page. That
  is intended: it is the absence of a sort rather than one of the five, and it was already the one
  order never written to a host's URL. Hosts setting `KildeSearch.Order` should know that a member
  now names a column rather than a direction, and that the new `Direction` parameter is `Ascending`
  by default whatever the order is — a struct has no unset state to read a per-column default out
  of, so a host that wants what a heading's first press gives passes
  `KildeSearch.InitialDirection(order)` alongside. `KildeExplorer` carries the new half in
  `?sortDir=`, omitted wherever the column is running the way that method says it runs, and `Sist
  endret` is reachable only once its column is turned on in the picker. (Fhi.Metadata-l9l2n.88)
