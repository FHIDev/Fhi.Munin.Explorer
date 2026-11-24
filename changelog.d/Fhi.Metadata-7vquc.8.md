category: Fixed
- **Catalogue text nested past the markdown parser's depth limit no longer takes the page down.** A stored value of 64
  nested `- ` or 128 nested `> ` markers made the parser throw inside the reader's circuit, with `CatalogueFormatting`
  on or off. Such a value now renders as its plain text, and a URL field holding one stays text.
