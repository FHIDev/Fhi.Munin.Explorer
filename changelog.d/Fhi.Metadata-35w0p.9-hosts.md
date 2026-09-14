category: Notes for hosts
- **The detail views' shared chassis adds four class names to style.** `munin-explorer-page` on the
  root of the kilde, datasamling and variable views, `munin-explorer-page__body` on the body, and
  `munin-explorer-page__main` and `munin-explorer-page__toc` on the two columns inside it — the last
  of those only on a page whose contents column is filled, which no page in this version is.
  Handles, all four: a host that defines nothing for them gets the body and its columns as blocks in
  ordinary flow, which is the single column those pages draw today. `Fhi.Helsedata.Stiler` has the rules
  already — the same `components/munin-explorer/_page.scss` the section wrapper landed in, extended
  by PR 39300 on 2026-09-11 with a `250px minmax(0, 1fr)` body above 1025px, a sticky contents
  column and a `min-inline-size: 0` main column — but that PR bumped no version either, so **no
  published Stiler carries them** and the body is a block whatever your pin says. Both sample
  stylesheets stand in for the rules at Stiler's own numbers. The one place they deliberately differ
  from what PR 39300 merged is the two-track rule, which they scope to
  `.munin-explorer-page__body:has(> .munin-explorer-page__toc)`: `munin-explorer-page__toc` is
  emitted only when something fills it and nothing does yet, so an ungated fixed first track would
  lay the main column out in 250px of a 1440px page. If you write the rule yourself, scope it the
  same way; `Fhi.Metadata-ex5wb` is the bead that puts the gate into Stiler's own copy before it
  publishes. (Fhi.Metadata-35w0p.9)
- **`DetailPage` is a new public component, and it is not one to mount.** It is the chassis the
  three detail views draw themselves in, public only because a Razor component has to be — the
  same reason `DetailSection` and `KildeHierarchyView` are. It takes the two class names the
  calling view wore before the chassis existed, a header, a contents column and the main content,
  and emits the wrappers described above; mounting one on your own page gets you three nested
  empty divs. (Fhi.Metadata-35w0p.9)
- **BREAKING for hosts: move any rule keyed on the detail views' `__body` names to
  `munin-explorer-page__body`.** `munin-explorer-kilde__body`, `munin-explorer-datasamling__body`
  and `munin-explorer-whole__body` are no longer emitted — the removal and why the element could
  not keep both names are under Removed. What to do about it is one move: whatever your rule
  declared, declare it on `munin-explorer-page__body` instead, and read the note above before you
  give it a second track. Every other old name stays exactly where it was:
  `munin-explorer-kilde`, `munin-explorer-datasamling` and `munin-explorer-whole` are still on the
  roots, `munin-explorer-kilde__main` and its two siblings still on the main column, and
  `munin-explorer-kilde__datasamlinger` still on the table it always named — that last one is why
  the prefixes were kept at all, being styled inside an expanded row of the kildeutforsker's result
  table as well as on the kilde page. A rule keyed on any of them draws what it drew before, and
  needs no new rule. (Fhi.Metadata-35w0p.9)
