category: Added
- **The filter panel explains its datakategori glyphs in words, under an `Ikonforklaring`
  legend.** The kilde tree draws one glyph per datakategori on a datasamling and a folder on the
  levels above, and until now the only place those pictures were named was the
  `screenreader-only` words on the row itself — so a sighted reader had nothing to read them by.
  The legend lists the whole vocabulary rather than what the narrowing has left on screen, built
  from `DataCategoryIcons.Order`, so it cannot pair a picture with the wrong word on a row that
  draws several; the grouping folder is deliberately absent, because it says only what the nesting
  already says. Every entry is localised in both `nb` and `en`. The glyphs stay `aria-hidden` and
  the meaning is ordinary text, not a `title` and not an `alt` — the same words for every reader —
  and they keep `currentColor`, so nothing here is told apart by hue alone. It is a `<details>`
  resting shut, whose `<summary>` carries the control's accessible name and expanded state, and it
  folds with the facets under `Utvid alle` and `Skjul alle`. It is drawn only while the `Ikoner`
  switch is on: with the pictures gone there is nothing left for it to explain. (Fhi.Metadata-zllxt)
