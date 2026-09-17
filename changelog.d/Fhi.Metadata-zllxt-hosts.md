category: Notes for hosts
- **Two class names to know about, and no rule of yours to write on a Stiler that has
  `components/munin-explorer/` from 0.1.75 onwards.** The legend wears
  `munin-explorer-filters__legend` on the list and `munin-explorer-filters__legend-item` on each
  row, and the glyph inside a row wears `munin-explorer-filters__icon` — the same name a facet
  value's glyph already wears, as a direct child of the row rather than inside the
  `munin-explorer-filters__icons` slot, which is what Stiler's rule for it selects. Both sample
  stylesheets already carried the rules before any markup wore the names, copied off the published
  0.1.75 and compared since against the 0.1.79 `samples/HostileHost` pins, so
  `scripts/assert-sample-css-matches-stiler.sh` against that pin is the whole of the evidence on
  this side that Stiler has them; nothing in this repository reads Stiler. Handles, both: the
  `<svg>` carries its own `width`, `height` and `stroke` and the names are real text, so a host
  that defines neither gets a list of glyphs and words at browser defaults and loses no word. What
  the rules buy is the row each pairing sits on and the columns the eighteen of them are laid out
  in. (Fhi.Metadata-zllxt)
