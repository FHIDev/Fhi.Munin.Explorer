category: Changed

- **The ordering moved into a column header, and the "Sorter etter" fieldset is gone** - helsedata
  and Runa both put sorting in the header, which is where a reader looks for it; keeping the
  fieldset as well would offer the same choice twice. The header is their own shape: a row wearing
  `variable-data-list__item__row--header`, with one `sortable-header` cell per column and Stiler's
  `hd-button-reset` on the buttons. Four of the five columns map to a real `SortField`; Periode has
  none, so its header is plain text rather than a button promising an ordering the API cannot do.
  The header renders whether or not the search found anything — it carries the ordering now, and
  taking it off screen mid-interaction would drop focus to `<body>`.
- **The columns each carry a per-column modifier, which is what a cell lines up by** - the widths
  hang off those names rather than off source order. The column SET is Runa's and is described in
  its own entry; this change is about the header they line up under. (Fhi.Metadata-zs56s,
  Fhi.Metadata-35oil)
