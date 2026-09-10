category: Added

- **The kilde detail hierarchy draws Kelda's node icons.** A delkilde wears a folder, a datasamling
  wears one glyph per datakategori it carries, and a variabelgruppe wears none - the same mapping
  Kelda's own tree uses, legacy category slugs and all, so the two surfaces cannot show one
  datasamling as two different things. Several categories draw in a fixed order and never twice; an
  unrecognised token draws the vocabulary's catch-all, while carrying no category at all draws
  nothing, because absence is not "Annet". The glyphs are decorative and `aria-hidden`, so a
  datasamling's categories are read out in words beside them instead. `ShowNodeIcons` on
  `KildeHierarchyView` and `KildeView` turns the icons off without touching the variable counts.
  (Fhi.Metadata-s3l7l)
