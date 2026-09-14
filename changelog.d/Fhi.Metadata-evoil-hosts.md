category: Notes for hosts
- **Two class names to style on the variable explorer's Kilde facet.**
  `munin-explorer-filters__icons` is the `<span>` holding a datasamling row's datakategori glyphs
  and `munin-explorer-filters__icon` is each inline `<svg>` inside it. Handles both: the `<svg>`
  carries `width`, `height` and `stroke="currentColor"` as attributes, so with no rule at all the
  glyphs still draw at text size in the text colour, and the slot is `aria-hidden` either way. What
  a rule buys is the row they sit in and the gap between them. Use `Fhi.Helsedata.Stiler` **0.1.75
  or later**, which supplies both (`Fhi.Metadata-1t36m`); both sample stylesheets carry the rules to
  copy for a host that has neither. One thing to keep if you write your own: the glyphs are drawn
  after the name, and a rule that moves them in front of it puts a variable number of glyphs in the
  indent column, where a row with three reads as a level deeper in the tree than a row with none.
  (Fhi.Metadata-evoil)
