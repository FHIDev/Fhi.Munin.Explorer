category: Notes for hosts

- **`munin-explorer-list-scroll` is new and needs `overflow-x: auto`.** It wraps the saved-list
  table alone, the way `munin-explorer-kilder-scroll` wraps the kilder table, and it is here for
  the same reason: nine columns do not fit a narrow viewport, and undrawn the overflow lands on
  the document, so the host's whole page scrolls sideways. The markup already carries `role`,
  `tabindex` and the table's own name, so the rule is all a host owes it. Both sample stylesheets
  carry it and the focus ring; `Fhi.Helsedata.Stiler` does not have it as of 0.1.37.
- **The saved list's rows are `<table>` markup now, so rules written against the old flex row miss
  them.** `munin-explorer-data-list` is still the name, but on a `<table>`, and the cells wear
  their per-column modifier without `munin-explorer-dataitem-main__column` — that class is
  `display: inline-flex`, which on a `<td>` takes the cell out of its own table. A host with rules
  of its own should scope them on the element: `VariableSearch` still draws the same names as a
  `<ul>` of flex rows and must keep doing so, because that shape is helsedata's own variable page.
  Stiler needs no change for the table to be legible — an element brings its own default where a
  class name brings nothing — but it has no rule for a `<table>` under this name either.
