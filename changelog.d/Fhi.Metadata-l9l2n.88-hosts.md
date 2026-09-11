category: Notes for hosts
- **The kilde table's sortable headings emit one new class name,
  `munin-explorer-kilder__sort`.** It is the button inside each of the four sortable `<th>`s, worn
  beside Stiler's own `hd-button-reset`, and it is a handle rather than a name that carries
  meaning: undefined, the heading is still a real button, still reachable by Tab, and the sorted
  column is still marked by the arrow beside its word and by `aria-sort` on the cell. What a rule
  buys is the hover underline and the colour on the sorted column. `Fhi.Helsedata.Stiler` adds this
  name to the two rules the variabelutforsker's header button already has — merged 2026-09-11 in PR
  39282, recorded in bead `Fhi.Metadata-l9l2n.106` — rather than giving it a block of its own, so
  the two cannot drift apart. Whether a published Stiler carries it cannot be read from this
  repository; nothing here reads Stiler. Both sample stylesheets show the rule.
  (Fhi.Metadata-l9l2n.88)
