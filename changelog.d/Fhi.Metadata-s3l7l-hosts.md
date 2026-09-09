category: Notes for hosts

- **The node icons need two names a host without Stiler must style**:
  `munin-explorer-hierarchy__icons`, the slot in front of a row's name, and
  `munin-explorer-hierarchy__icon`, each glyph in it. Both sample stylesheets show the pair. An
  undefined one is not an unstyled one: every glyph carries `width="1em"`, `height="1em"` and
  `stroke="currentColor"` of its own, so it draws at text size in the text colour rather than at an
  SVG's 300x150 default. What is lost is the colour that tells two datakategorier apart at a
  glance, and the rule to write it with is `data-node-icon` on the glyph - `PHDR`, `EINS`, `other`
  and the rest of the EHDS codes, plus `kilde` on the grouping folder. helsedata's own appearance
  is not in this release: it ships from `Fhi.Helsedata.Stiler` under `Fhi.Metadata-wihod`.
  (Fhi.Metadata-s3l7l)
