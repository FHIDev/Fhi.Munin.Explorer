category: Notes for hosts

- **One lane in the sidebar is the host stylesheet's job, and `Fhi.Helsedata.Stiler` has done it
  since 0.1.39.** The fact lists in the kilde, datasamling and whole-variable asides wear
  `munin-explorer-meta__grid`, the same class the package draws every fact list with. That grid is
  two `1fr` tracks and `1fr` floors at min-content, so in a 320px sidebar the Lovverk prose sizes the
  tracks past the panel edge, and the reader sees the whole page scroll sideways at any width above
  1280px. Stiler closes it with a rule scoped to the three asides —
  `.munin-explorer-kilde__aside .munin-explorer-meta__grid` and its `__whole__`/`__datasamling__`
  siblings, `grid-template-columns: minmax(0, 1fr)` — and the sample stylesheets here now carry a
  copy of that rule, selector for selector, so the stand-in works the way the real one does. A host
  on an older Stiler, or on a stylesheet of its own, needs the equivalent or it gets the scrollbar.
- **Reach it by scoping to the aside, not by the `munin-explorer-meta__grid-1` modifier.** The
  package still writes that name in one place — the variable detail panel, where helsedata's own
  layout expects it — and in none of the asides, so a host should not reach for it there either. In
  Stiler the same name also carries `grid-row: 1/3` for the variable page's layout, so on an aside it
  brings a placement rule along with the single track. The scoped rule out-specifies it there
  regardless. (Fhi.Metadata-hi0po)
