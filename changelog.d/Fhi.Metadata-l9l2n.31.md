category: Fixed
- Catalogue properties are drawn according to the type the catalogue declares for them, so a
  value stored as structure no longer reaches the page as JSON. `MultilingualText` and
  `LangTaggedList` resolve to the reader's language, `MultiSelect` resolves each of its codes
  through the property's own vocabulary instead of matching the whole array against it, and
  `Object` — which has a curated label but no curated parts — drops its row rather than printing
  the record. A value that is not the shape its type promises is still shown as it arrived.
- Rows carrying a multilingual value now report the language they resolved to, so an English
  title is no longer marked `lang="no"` and read aloud in a Norwegian voice.
