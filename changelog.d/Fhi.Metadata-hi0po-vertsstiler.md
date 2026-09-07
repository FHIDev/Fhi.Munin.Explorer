category: Notes for hosts

- **`munin-explorer-meta__grid-1` is now written in a second place, and a rule scoped to the first
  one will not reach it.** The single-lane modifier used to appear only inside the variable detail
  panel, under `munin-explorer-meta`. It is now also on the fact lists in the kilde, datasamling and
  whole-variable asides, which sit under `munin-explorer-kilde__aside` and its siblings inside
  `munin-explorer-drilldown` — no `munin-explorer-meta` ancestor anywhere above them. A host whose
  rule reads `.munin-explorer-meta .munin-explorer-meta__grid-1` therefore styles nothing here, and
  the sidebar keeps the two-lane default that made the page scroll sideways. Check the selector, not
  the name.
- **It needs to beat the base class, and it only has equal specificity to do it with.** Both are
  single-class selectors, so `munin-explorer-meta__grid-1` wins only where it is declared after
  `munin-explorer-meta__grid`'s own `grid-template-columns`. The sample stylesheets get that right by
  source order; a host that declares the base last loses the modifier silently.
  (Fhi.Metadata-hi0po)
