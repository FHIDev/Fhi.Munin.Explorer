category: Notes for hosts

- **`munin-explorer-list-scroll` is new, and a host owes it a `:focus-visible` outline.** It wraps
  the saved-list table alone, the way `munin-explorer-kilder-scroll` wraps the kilder table. Unlike
  that one it carries its own `overflow-x: auto` inline: measured in `HostileHost`, nine columns
  put 1323px of table in an 843px page and the *document* scrolled, which is WCAG 1.4.10 on the
  host's page rather than a table that looks wrong on ours — and no host stylesheet can be assumed
  to have the name the day it appears. What is still the host's is the focus ring, because the box
  carries `tabindex="0"` and a focus stop nobody can see is WCAG 2.4.7. Both sample stylesheets
  have it; `Fhi.Helsedata.Stiler` has neither rule as of 0.1.37.
- **The saved list's rows are `<table>` markup now, so rules written against the old flex row miss
  them.** `munin-explorer-data-list` is still the name, but on a `<table>`, and the cells wear
  their per-column modifier without `munin-explorer-dataitem-main__column` — that class is
  `display: inline-flex`, which on a `<td>` takes the cell out of its own table. A host with rules
  of its own should scope them on the element: `VariableSearch` still draws the same names as a
  `<ul>` of flex rows and must keep doing so, because that shape is helsedata's own variable page.
  Stiler needs no change for the table to be legible — an element brings its own default where a
  class name brings nothing — but it has no rule for a `<table>` under this name either.
