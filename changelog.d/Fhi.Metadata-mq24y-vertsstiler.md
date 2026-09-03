category: Notes for hosts

- **Three new class names need rules: `munin-explorer-kilder__expand`,
  `munin-explorer-kilder__expand-toggle` and `munin-explorer-kilder__expanded`.** The first is the
  control column and needs a width; the toggle wears `hd-button-reset`, so without a rule it is a
  bare glyph with no hit area of its own; the third is the expanded row's cell, which needs padding
  to read as a panel rather than as another table row. A host that skips them still gets a working
  toggle and a readable list — this is look, not information. The rule for them in
  `Fhi.Helsedata.Stiler` is tracked as its own bead.
