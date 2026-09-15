category: Notes for hosts
- **Four more bare section ids: `variables`, `accesscriteria`, `prices` and `codelists`.** They
  join the nine the detail views already write without a per-instance discriminator, for the same
  reason, so the same bound applies: a page that already means something of its own by
  `id="prices"` will have a duplicate, and the nav's link lands on whichever comes first.
  `Fhi.Metadata-uobxg` is where that bound gets settled. (Fhi.Metadata-fkiz9)
- **Kelda's sections and the whole variable's Kodeverk now sit in `munin-explorer-page__section`
  wrappers, so `Fhi.Helsedata.Stiler`'s rules for that name reach them.** No new class name. Two of
  those rules change what these sections look like under Stiler: `scroll-margin-top` puts a heading
  reached from the nav clear of a sticky header, and the `:last-child` rule gives the last section
  on a kilde page — Variabler, or Priser with `ShowAccessAndPrices` — the `min-height` that lets the
  nav's last link scroll its section into view. Runa's kilde page already ended in a wrapped
  section. (Fhi.Metadata-fkiz9)
