category: Fixed
- **`KildeExplorer` keeps the kilde list's search, facets, ticks and columns in the address, so a
  round trip away from the list comes back to it as it was left.** The "Kilder" crumb over an open
  kilde, "Tilbake til kilde" out of a datasamling and browser Back after "Utforsk variabler for
  utvalget" all used to land on an empty list. The list's state is now written with
  `history.replaceState` as `?search=`, one repeated key per facet (`?kildetype=`, `?kategori=`,
  `?tilgangsniva=`, `?databehandler=`), `?columns=` and `?selected=` — the names Munin's own Kelda
  uses — and every link `KildeExplorer` builds carries it, so a copied link opens the same view. A
  key at its default is not written, and a bare address still opens an untouched list. Nothing is
  kept in `sessionStorage` or `localStorage`. (Fhi.Metadata-nvf2w, sak #5689)
