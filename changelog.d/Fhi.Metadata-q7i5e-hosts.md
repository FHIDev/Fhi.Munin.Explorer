category: Notes for hosts
- **Two new names size the save column: `munin-explorer-dataitem-main__save` on the button's
  cell and `munin-explorer-dataitem-header__save` on the header cell above it.** Give both one
  fixed width, `flex: 0 0 10rem`, where the result row is a flex row. Without it the cell sizes
  to its button, so the "Variabelliste" header does not sit over the buttons, and pressing one
  shifts the later columns in its row as the label changes length. The rule ships in
  `Fhi.Helsedata.Stiler` under Fhi.Metadata-6u1iu, together with the header geometry the
  alignment depends on: the header row loses its 36px left padding, Navn's header button takes a
  3rem indent instead, and the Kode, Datatype and Status header cells get the 16px left padding
  their values have. Until a host's pinned Stiler carries that, the "Variabelliste" header shows
  but the header cells sit out of line with the values under them. (Fhi.Metadata-q7i5e)
