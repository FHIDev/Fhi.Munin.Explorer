category: Notes for hosts
- **The detail views emit four new class names, and an empty contents column with them.**
  `munin-explorer-page` on the root of the kilde, datasamling and variable views,
  `munin-explorer-page__body` on the body, and `munin-explorer-page__main` and
  `munin-explorer-page__toc` on the two columns inside it. Handles, all four: a host that defines
  nothing for them gets the body and its columns as blocks in ordinary flow, which is the single
  column those pages draw today. `Fhi.Helsedata.Stiler` has the rules already — the same
  `components/munin-explorer/_page.scss` the section wrapper landed in, extended by PR 39300 on
  2026-09-11 with a `250px minmax(0, 1fr)` body above 1025px, a sticky contents column and a
  `min-inline-size: 0` main column — but that PR bumped no version either, so **no published
  Stiler carries them** and the body is a block whatever your pin says. Both sample stylesheets
  stand in for the rules at Stiler's own numbers. The part to read twice is the contents column:
  it is emitted whether anything fills it or not, because its track is a fixed 250px and a body
  holding only the main column would lay that column out in it — so a host that does style the
  chassis gets a 250px empty rail beside the content until the contents nav that fills it ships.
  (Fhi.Metadata-35w0p.9)
- **`DetailPage` is a new public component, and it is not one to mount.** It is the chassis the
  three detail views draw themselves in, public only because a Razor component has to be — the
  same reason `DetailSection` and `KildeHierarchyView` are. It takes the two class names the
  calling view wore before the chassis existed, a header, a contents column and the main content,
  and emits the wrappers described above; mounting one on your own page gets you three nested
  empty divs. (Fhi.Metadata-35w0p.9)
- **One name in three spellings is gone: the detail views' `__body`.**
  `munin-explorer-kilde__body`, `munin-explorer-datasamling__body` and `munin-explorer-whole__body`
  are no longer emitted; the element they named now wears `munin-explorer-page__body` alone. A
  rule of yours keyed on one of them stops matching, and the reason it has to is that every
  published Stiler still lays those three out as `minmax(0, 1fr) 320px` — the track the aside used
  to fill until `Fhi.Metadata-35w0p.6` stopped emitting one. The chassis always emits a contents
  column, which is a second child for that second track to catch, and the kilde page came back
  with its whole main column in the 320px rail. Move any such rule to
  `munin-explorer-page__body`. Every other old name stays exactly where it was:
  `munin-explorer-kilde`, `munin-explorer-datasamling` and `munin-explorer-whole` are still on the
  roots, `munin-explorer-kilde__main` and its two siblings still on the main column, and
  `munin-explorer-kilde__datasamlinger` still on the table it always named — that last one is why
  the prefixes were kept at all, being styled inside an expanded row of the kildeutforsker's result
  table as well as on the kilde page. A rule keyed on any of them draws what it drew before, and
  needs no new rule. (Fhi.Metadata-35w0p.9)
