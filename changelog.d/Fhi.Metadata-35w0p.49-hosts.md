category: Notes for hosts
- **`munin-explorer-page__eyebrow` and `munin-explorer-page__actions` are new class names, and
  `Fhi.Helsedata.Stiler` 0.1.75 already carries a rule for both.** They are the detail chassis's
  chrome above the name block — the word naming the kind of page, and the row of page-level
  controls. A host on 0.1.75 or later needs to do nothing. Handles, both: undefined, the eyebrow is
  a paragraph above the title and the action row is its children in ordinary flow, so what is lost
  is the pill and the spacing rather than any word. Both sample stylesheets stand in at exactly
  what the pinned 0.1.75 declares. (Fhi.Metadata-35w0p.49)
- **The breadcrumb wears helsedata's own `breadcrumbs` names and adds none of ours.** `breadcrumbs`
  on the `<nav>`, `breadcrumbs__list` on the `<ol>`, `breadcrumbs__list-item` on each step,
  `breadcrumbs__divider` on the separator and `breadcrumbs__last-crumb` on the current page — all
  global, unscoped classes in Stiler's `layout/_breadcrumbs.scss`, so a host with Stiler gets the
  site's own trail for nothing. **If your stylesheet writes the list rule against `ul` rather than
  against the class, it will not match**: the list is an `<ol>`, because the steps are ordered and
  that is what a breadcrumb tells a screen reader. Unstyled it is a numbered list, which still
  reads correctly. Neither sample host stands these five in — a partial copy of a borrowed rule is
  a divergence, not a stand-in. (Fhi.Metadata-35w0p.49)
