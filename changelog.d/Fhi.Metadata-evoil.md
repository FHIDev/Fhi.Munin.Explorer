category: Added
- **A datasamling in the Kilde facet now shows one glyph per datakategori it carries.** The same
  EHDS glyphs and the same shared render order the kildeutforsker's hierarchy tree already draws,
  read off the `categories` field of the `GET /api/explorer/filters` answer the row itself is built
  from — so no kilde hierarchy is fetched to draw them and opening a branch still costs no request.
  A datasamling hanging straight off its kilde is drawn exactly as one under a delkilde; `DelkildeId`
  decides where the row hangs and nothing about the glyphs. The glyphs follow the name rather than
  leading it, so a row with three of them starts where a row with none does and the tree's apparent
  depth is unchanged. A token the icon table does not know falls back to the catch-all glyph, as it
  does in the hierarchy tree, and a datasamling with no categories at all draws nothing. The slot is
  `aria-hidden` and the categories are named in `screenreader-only` words after the label instead,
  the way the hierarchy tree names them, so the checkbox says which datakategorier its datasamling
  carries rather than leaving the pairing to the glyphs alone.
  (Fhi.Metadata-evoil)
