category: Notes for hosts

- **One new class name for the variabelutforsker's Kilde facet, `munin-explorer-filters__search`,
  styled from `Fhi.Helsedata.Stiler` PR 39236.** It is the box that narrows that facet's own values.
  A handle: a host that defines nothing for it gets a browser-default search field, which is
  visible, operable and named by a `<label>` of its own, so what a rule buys is the box — full
  width in a sidebar column, 34px tall and at the panel's own type size rather than the page's.
  A host writing its own rule owes it one thing that is easy to miss: **lead the selector with the
  element**, `input.munin-explorer-filters__search`. Stiler's global `input[type="search"]` list is
  (0,1,1) and a bare class is (0,1,0), so a class-only rule loses its `font-size` to that list and
  the field draws at 18px where 14px was measured — both figures measured on the real stylesheet
  rather than derived. The rule merged to Stiler's `main` as `0bd0b34` on 2026-09-09 with no version
  bump, so the floor is the first release that follows 0.1.42 — a host on 0.1.42 or older is in the
  undressed case above rather than a broken one. Both sample stylesheets carry the stand-in, declaration for
  declaration. The kildetype groups that appear inside the same facet add no name at all: they are
  `<details>`/`<summary>` like the facets around them, so their marker, open state and focus ring
  come from the `.munin-explorer-filters summary` rules a host already has, and their counts wear
  `munin-explorer-filters__chosen`, which the kildeutforsker's summaries introduced.
  (Fhi.Metadata-l9l2n.67)
