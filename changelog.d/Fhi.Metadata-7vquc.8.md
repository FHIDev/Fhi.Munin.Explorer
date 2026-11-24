category: Fixed
- **Catalogue text nested too deep no longer takes the page or the host down.** A stored value of 64 nested `- ` or
  128 nested `> ` markers made the markdown parser throw inside the reader's circuit, and about 10 000 nested `![`
  overflowed the stack while drawing, which ends the host process, with `CatalogueFormatting` on or off. A value
  whose parsed tree is deeper than 128 levels now renders whole as its plain text, its links included, and a kilde
  or datasamling description of that kind is not folded behind «Les mer». The cap counts every kind of nesting
  together; on its own it is reached by, for example, 63 nested list markers, 126 nested quote markers, or about
  125 unclosed `[` or levels of `*` emphasis in one paragraph (the count depends on what follows them).
