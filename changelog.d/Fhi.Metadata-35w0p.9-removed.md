category: Removed
- **BREAKING for hosts: `munin-explorer-kilde__body` and its two siblings are no longer emitted.**
  `munin-explorer-datasamling__body` and `munin-explorer-whole__body` go with it. The element all
  three named — the grid the detail pages lay their columns out in — now wears
  `munin-explorer-page__body` alone, so a rule of yours keyed on any of the three stops matching and
  has to move to the chassis name. They are the only names this change drops. The element could not
  keep both, which is the whole reason: every published `Fhi.Helsedata.Stiler` lays those three out
  as a grid of their own, so an element wearing an old name and the chassis name would carry a
  `grid-template-columns` from each block, and which one drew would be settled by the order a host
  happened to load the two stylesheets in rather than by either of them meaning it.
  (Fhi.Metadata-35w0p.9)
