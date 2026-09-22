category: Notes for hosts
- **New class name `munin-explorer-dataitem__expand-toggle` on Runa's row chevron, styled by
  Fhi.Helsedata.Stiler from the release that carries Fhi.Metadata-35w0p.65 and .67.** It is a
  `<button class="hd-button-reset munin-explorer-dataitem__expand-toggle">`, the first direct child
  of `.munin-explorer-dataitem-main`, holding only the existing
  `munin-explorer-dataitem-main__expand-icon` glyph. Stiler sizes it to 40x32, keeps it visible
  below 1280px where the row's other icons are hidden, draws its focus surface, and moves the header
  indent to 52px while it is present. On an older Stiler the glyph is hidden below 1280px and the
  button is an empty focusable box, so upgrade Stiler with this version. (Fhi.Metadata-35w0p.34)
