category: Notes for hosts
- **Four more bare section ids: `variables`, `accesscriteria`, `prices` and `codelists`.** They
  join the nine the detail views already write without a per-instance discriminator, for the same
  reason, so the same bound applies: a page that already means something of its own by
  `id="prices"` will have a duplicate, and the nav's link lands on whichever comes first.
  `Fhi.Metadata-uobxg` is where that bound gets settled. (Fhi.Metadata-fkiz9)
- **Kelda's sections and the whole variable's Kodeverk now sit in `munin-explorer-page__section`
  wrappers, so `Fhi.Helsedata.Stiler`'s rules for that name reach them.** No new class name.
  `scroll-margin-top` puts a heading reached from the nav clear of a sticky header. And unless your
  `KildeSearch.Sections` draws an element, the last of Kelda's sections — Variabler, or Priser with
  `ShowAccessAndPrices` — is now the column's last child, so Stiler's `:last-child` rule gives it a
  `min-height` of `calc(60vh - 120px)`: expect that much space under a one-line section at the
  bottom of an open kilde. Stiler's reason is a scroll-spy, which could otherwise never mark the
  nav's last entry active; Runa's kilde page already ended in a wrapped section and got the same
  space. (Fhi.Metadata-fkiz9)
