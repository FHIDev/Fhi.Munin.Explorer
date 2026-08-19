category: Changed

- **Column widths follow Runa's proportions, and a code never wraps** - Kode is the widest column,
  which looks wrong until you notice a variable code is one unbreakable token: broken across two
  lines it stops being readable and stops being copyable. A name has spaces, so the name is the
  column that gives way. Widths are Runa's, measured off it — Navn 210, Kode 246, Kilde 96,
  Datasamling 212, Variabelgruppe 160, Datatype 114, Status 98 — expressed as flex ratios so they
  hold at any width. The code column truncates with an ellipsis rather than wrapping, and every
  cell carries its full value as a tooltip.
- **The Kilde column shows the short name** - "ALS" rather than "Als registeret", with the full name
  on hover, exactly as Runa does. A kilde name is long and repeats down every row of one register's
  variables. It falls back to the full name where a kilde has no short one.
- **Field names are read to assistive technology without being shown** - each cell carries its label
  in Stiler's `screenreader-only` span. A screen reader moving down a column has no header to glance
  up at, so the name has to travel with the value. (Fhi.Metadata-zs56s)
