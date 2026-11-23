category: Added
- **Catalogue text can show bold, italic and lists, behind `MuninExplorer:Features:CatalogueFormatting`.** With the
  flag on, the descriptions and the authored fields (Kommentar, Kvalitetsnote, Formål and the rest of
  `CatalogueProperties.AuthoredKeys`) draw `**bold**` and `*italic*` as `strong` and `em`, and lists written with `-`
  or `1.` as `ul` and `ol`, nested as written up to three deep (FHIDev/Munin#7449). A lone `-` stays text, and a long
  kilde or datasamling description holding a list folds behind «Les mer» after a sentence of its first paragraph or
  between blocks, never inside the list. It is still a walk of the parsed text into Blazor's render tree: raw HTML
  other than `<br>`, headings, code, `_` emphasis, `*` and `+` bullets, `1)` lists and `javascript:` links stay text.
  Off by default; with it off nothing changes.
