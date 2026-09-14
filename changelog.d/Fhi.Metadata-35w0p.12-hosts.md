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
- **The nav itself borrows helsedata's `form-menu__list` and `form-menu__list__item`, and needs no
  new rule from anyone.** Both are global, unscoped selectors in Stiler's
  `pages/_healthregisterpage.scss`, so on a host carrying Stiler the nav takes that site's own link
  colour, padding and hover for nothing. A host without those rules gets a plain unstyled list of
  links, which still reads as a contents list. Nothing is emitted for the active-item modifiers
  `form-menu__list__item--active` and `--active-child`: with no scroll tracking in this package an
  active item would be permanently wrong on every section but one. (Fhi.Metadata-35w0p.12)
- **`DetailToc` and `DetailTocEntry` are new public types, and neither is one to mount.**
  `DetailToc` is the nav the three detail views put in their own contents column, public only
  because a Razor component has to be — the same reason `DetailPage` and `DetailSection` are — and
  `DetailTocEntry` is the id-and-label pair it takes. Mounting one yourself gets a `<nav>` of links
  to whatever ids you hand it. (Fhi.Metadata-35w0p.12)
