category: Notes for hosts
- **The hierarchy's node icons are not meant to be coloured per datakategori.** `ShowNodeIcons`'s
  documentation and the README said a host stylesheet was what made a category recognisable at a
  glance. That was never the delivered design: helsedata's Stiler gives no datakategori a colour of
  its own, since the shape and the spoken words already tell them apart, and mutes only the delkilde
  folder (`data-node-icon="kilde"`). The same docs said the glyph draws in front of the name: the
  package writes the slot there, and helsedata's stylesheet paints the hierarchy's after the name
  and count, the facet panel's before it. Nothing in the markup changed.
