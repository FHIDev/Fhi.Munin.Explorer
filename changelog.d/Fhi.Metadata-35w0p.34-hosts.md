category: Notes for hosts
- **New class names `munin-explorer-dataitem__expand-cell` and `munin-explorer-dataitem__expand-toggle`
  on Runa's row chevron, styled by Fhi.Helsedata.Stiler from the release that carries
  Fhi.Metadata-35w0p.72.** The chevron is a
  `<button class="hd-button-reset munin-explorer-dataitem__expand-toggle">` holding only the existing
  `munin-explorer-dataitem-main__expand-icon` glyph, inside a
  `<div role="cell" class="munin-explorer-dataitem__expand-cell">` that is the first direct child of
  `.munin-explorer-dataitem-main`. The cell is there because a table row may own only cells. Stiler
  sizes the button to 40x32, keeps it visible below 1280px where the row's other icons are hidden,
  draws its focus surface, and moves the header indent to 52px while it is present. On a Stiler
  without that release the glyph is hidden below 1280px and the button is an empty focusable box, so
  upgrade Stiler with this version. (Fhi.Metadata-35w0p.34)
