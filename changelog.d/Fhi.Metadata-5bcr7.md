category: Fixed
- The catalogue's authored markup now renders instead of printing as source. The kilde and
  datasamling descriptions and the datasamling table's description column turn markdown links
  into real links and `<br>` tags and bare newlines into line breaks, and a property the
  catalogue types as a `Url` — Hjemmeside is the one readers meet — becomes a followable link
  instead of a `[label](url)` printed whole. The grammar is deliberately that small: the text is
  parsed with Markdig and the AST is walked straight into the render tree, so no raw-HTML
  pathway exists — a heading, emphasis, a `javascript:` link or any HTML tag renders as literal
  text, links carry `rel="noopener noreferrer"` and only `http`, `https` and `mailto` schemes
  become anchors, and text over 20 000 characters is not parsed at all.
