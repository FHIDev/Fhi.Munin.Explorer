category: Notes for hosts
- **Two new names size the save column: `munin-explorer-dataitem-main__save` on the button's
  cell and `munin-explorer-dataitem-header__save` on the header cell above it.** Give both one
  fixed width, `flex: 0 0 10rem`, where the result row is a flex row. Without it the cell sizes
  to its button, so the "Variabelliste" header does not sit over the buttons, and pressing one
  shifts the later columns in its row as the label changes length. The rule ships in
  `Fhi.Helsedata.Stiler` under Fhi.Metadata-6u1iu, together with the header geometry the
  alignment depends on: the header row loses its 36px left padding, Navn's header button takes a
  3rem indent instead, and the Kode, Datatype and Status header cells get the 16px left padding
  their values have. A failed save's sentence is now its own cell of the row,
  `munin-explorer-data-list__save-status`, after the column strip: `.munin-explorer-data-list__item__row`
  wraps, the cell takes the full width, and its alert is indented `3rem` with `12px` below while it
  has text. Until a host's pinned Stiler carries that, the "Variabelliste" header shows but the
  header cells sit out of line with the values under them, and a failed save's sentence sits in a
  narrow cell beside the row's columns, squeezing that one row, instead of on a line below it.
  (Fhi.Metadata-q7i5e)
