category: Notes for hosts

- **Three new class names need rules: `munin-explorer-frequency`, `munin-explorer-frequency__track`
  and `munin-explorer-frequency__fill`.** The first is the categorical frequency table and needs
  only what a host gives its other tables. The other two are the share bar and carry meaning
  nothing else carries: `__fill` is an inline element whose width is set per row, so without
  `display` and a height it draws nothing at all and the bar simply disappears. The percentage is
  written beside it as text, so a host that skips these loses the visual encoding rather than the
  fact. The rule for them in `Fhi.Helsedata.Stiler` is tracked as its own bead.
