category: Notes for hosts

- **The datasamlinger table needs two column widths.** Without them `table-layout: auto` hands the
  width to the description — it is catalogue free text and always the longest — and Gyldighet and
  Antall variabler wrap in every row. Both sample hosts' `host.css` now sets `width: 22%` on the
  third column and `width: 1%` with `white-space: nowrap` on the fourth body cell, and
  Fhi.Helsedata.Stiler carries the same two rules in
  `Static/scss/components/munin-explorer/_trail.scss`. The name column is deliberately left to wrap.
  (Fhi.Metadata-oq40w)
