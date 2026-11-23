category: Added
- **Catalogue text can show bold, italic and lists, behind `MuninExplorer:Features:CatalogueFormatting`.** With the
  flag on, the descriptions and the authored fields (Kommentar, Kvalitetsnote, Formål and the rest of
  `CatalogueProperties.AuthoredKeys`) draw `**bold**` and `*italic*` as `strong` and `em`, and lists written with `-`
  or `1.` as `ul` and `ol`, nested one level where written so (FHIDev/Munin#7449). It is still a walk of the parsed
  text into Blazor's render tree: raw HTML other than `<br>`, headings, code, `_` emphasis, `*` and `+` bullets,
  `1)` lists and `javascript:` links stay text. Off by default; with it off nothing changes.
