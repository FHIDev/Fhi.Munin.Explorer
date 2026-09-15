category: Notes for hosts
- **Two class names to style on the variable explorer's Kilde facet.**
  `munin-explorer-filters__icons` is the `<span>` holding a datasamling row's datakategori glyphs
  and `munin-explorer-filters__icon` is each inline `<svg>` inside it. Handles both: the `<svg>`
  carries `width`, `height` and `stroke="currentColor"` as attributes, so with no rule at all the
  glyphs still draw at text size in the text colour, and the slot is `aria-hidden` either way. What
  a rule buys is the row they sit in and the gap between them. Use `Fhi.Helsedata.Stiler` **0.1.75
  or later**, which supplies both (`Fhi.Metadata-1t36m`); both sample stylesheets carry the rules to
  copy for a host that has neither. The glyphs are drawn between the checkbox and the name, as in
  Runa. Preserve that order and keep checkbox indentation independent of the number of glyphs.
  (Fhi.Metadata-evoil)
