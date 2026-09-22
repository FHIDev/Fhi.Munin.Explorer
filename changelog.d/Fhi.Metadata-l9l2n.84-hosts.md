category: Notes for hosts
- **A host styling the row chevrons needs `Fhi.Helsedata.Stiler` 0.1.91 or later.** 0.1.91 is
  where the rules keyed on `[aria-expanded=false]` landed, and they are what tell a collapsed
  `icon-keyboard-arrow-down` from an expanded one while the older, inverted overrides for the
  previous names are still in the stylesheet. On an earlier Stiler the collapsed chevron is drawn
  by one of those overrides and points up. A host supplying its own rules for
  `munin-explorer-kilder__expand-icon` or `munin-explorer-dataitem-main__expand-icon` should key
  them on the class names above rather than on `icon-keyboard-arrow-right`, which the component no
  longer emits. (Fhi.Metadata-l9l2n.84)
