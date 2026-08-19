category: Changed

- **The ordering moved into a column header, and the "Sorter etter" fieldset is gone** - helsedata
  and Runa both put sorting in the header, which is where a reader looks for it; keeping the
  fieldset as well would offer the same choice twice. The header is their own shape: a row wearing
  `variable-data-list__item__row--header`, with one `sortable-header` cell per column and Stiler's
  `hd-button-reset` on the buttons. Four of the five columns map to a real `SortField`; Periode has
  none, so its header is plain text rather than a button promising an ordering the API cannot do.
  The header renders whether or not the search found anything — it carries the ordering now, and
  taking it off screen mid-interaction would drop focus to `<body>`.
- **The result columns are helsedata's five, so Kode moved into the panel** - source, period,
  dataCollection and theme, in their order, each carrying the per-column modifier their grid widths
  hang off. Their row has no code column, and adding a sixth would have meant inventing a class
  name with no rule behind it. The code is still on screen, one click away, and searching by code
  still finds the variable. (Fhi.Metadata-zs56s, Fhi.Metadata-35oil)
