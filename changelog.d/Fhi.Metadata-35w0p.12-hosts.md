category: Notes for hosts
- **`munin-explorer-page__toc` is now drawn on every detail page, so its rule finally matters.**
  It was emitted by nothing until this version — the note under 0.1.x's chassis entry said so — and
  the contents nav fills it now. The rules are `Fhi.Helsedata.Stiler`'s, in
  `components/munin-explorer/_page.scss`: a `250px minmax(0, 1fr)` body above 1025px and a sticky
  contents column. A host that defines nothing for the name still loses no words — the column
  becomes a block in ordinary flow above the main one, which is a contents list above the content
  it lists. Both sample stylesheets carry the stand-in, scoped to a body that has a contents column
  (`Fhi.Metadata-ex5wb` is the bead that puts that gate into Stiler's own copy), because the column
  is still drawn only when something fills it and a page with no sections fills nothing.
  (Fhi.Metadata-35w0p.12)
- **The nav itself borrows helsedata's `form-menu__list` and `form-menu__list__item`, and Stiler
  declares them, so you write no rule for either.** They are global, unscoped classes in Stiler's
  `pages/_healthregisterpage.scss` and they reach its compiled stylesheet, which is where the nav
  gets its link colour and padding from. Both sample stylesheets carry a stand-in for hosts with no
  Stiler, and that stand-in now declares what the pinned 0.1.42 declares — `margin` on the list,
  `background-color` and `transition` on the link — after
  `scripts/assert-sample-css-matches-stiler.sh` found it three declarations short of the real rule.
  `form-menu__list__item` has no rule of its own anywhere, and nothing is emitted for the
  active-item modifiers `form-menu__list__item--active` and `--active-child` either: with no scroll
  tracking in this package an active item would be permanently wrong on every section but one.
  (Fhi.Metadata-35w0p.12)

- **One detail view per document.** The section ids the nav links to — `metadata`, `source`,
  `statistics` and the rest — are fixed English literals with nothing per-instance in them, so that
  a deep link one reader sends another lands in the same place whichever language either is
  reading. The price is that two detail views in one document write each id twice, and a browser
  resolves a fragment to the first match: the second view's contents nav would scroll the reader
  into the first view's sections. Mount one. `VariableSearch` and `KildeSearch` already do — each
  picks between the arms of one `if`/`else`. (Fhi.Metadata-35w0p.12)
- **`DetailToc` and `DetailTocEntry` are new public types, and neither is one to mount.**
  `DetailToc` is the nav the three detail views put in their own contents column, public only
  because a Razor component has to be — the same reason `DetailPage` and `DetailSection` are — and
  `DetailTocEntry` is the id-and-label pair it takes. Mounting one yourself gets a `<nav>` of links
  to whatever ids you hand it. (Fhi.Metadata-35w0p.12)
