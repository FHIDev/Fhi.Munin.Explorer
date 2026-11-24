category: Fixed
- **Catalogue text nested too deep no longer takes the page or the host down.** A stored value of 64 nested `- ` or
  128 nested `> ` markers made the markdown parser throw inside the reader's circuit, and about 10 000 nested `![`
  overflowed the stack while drawing, which ends the host process, with `CatalogueFormatting` on or off. A value
  whose parsed tree is deeper than 128 levels now renders as its plain text, and a URL field holding one stays
  text. That is 63 or more nested list markers, 126 or more nested quote markers, or 126 or more unclosed `[` in one
  paragraph, whose links then stay text as well.
