category: Fixed
- **A variable's description renders its links and line breaks instead of printing them as
  source.** The variable page's ingress, the description in Runa's row panel and each version's
  description in the version history printed the catalogue's `<br>` tags and `[label](url)` links
  literally, while the kilde, delkilde and datasamling descriptions already rendered them. All
  three now go through the same renderer: links and line breaks only, no raw HTML, so a
  description holding an HTML tag still shows the tag as text. The ingress keeps its `lang` and
  class, and no class name is added. (Fhi.Metadata-35w0p.46)
