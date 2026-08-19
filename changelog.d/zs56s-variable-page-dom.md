category: Changed

- **The results now wear helsedata's variable-page vocabulary instead of their datakilde cards** -
  the component was built from `datasourcecard*`, which is their *datakilde* explorer. We replace
  the *variable* explorer, and that page has its own: `variable-data-list__item` rows inside
  `variable-explorer-container`, with `variable-meta` for the opened panel. The switch is not a
  rename — 132 of the 292 selectors in that family are descendant selectors, so the nesting has to
  match or roughly half the styling silently does not apply. (Fhi.Metadata-zs56s)
- **A result row is opened by its own name, and the dead click target is gone** - the variable's
  name is now the disclosure button, which is helsedata's pattern and the APG accordion pattern.
  The old card advertised a click it did not have: `.datasourcecard` carries a pointer cursor
  because on their datakilde page the whole card is a link, and ours never was. There is no heading
  around the button: their row is a flex container and the name cell is sized by
  `variable-dataitem-main__name`, so a heading in between becomes the flex item and the column stops
  lining up with its header. Results are a list of list items, each with a named disclosure carrying
  `aria-expanded`. (Fhi.Metadata-ywnbs)
