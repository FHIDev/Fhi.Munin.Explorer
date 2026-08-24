category: Notes for hosts
- **The pager's skip link needs a class name that is not in Stiler** - `skiplink-pagination`, read
  back off the compiled stylesheets. Stiler has no pagination rule of any kind — no `pagination`,
  `pager`, `paging`, `page-link` or `page-item` — while helsedata's own variable page styles its
  pager from a `variables.css` the site-wide stylesheet does not carry. The pager itself wears our
  own `munin-explorer-pagination*` names and Stiler carries their rules; the skip link still wears
  helsedata's, so a host outside their estate has to supply it, including the rule that keeps it
  out of sight until it is focused. Both sample hosts show a working approximation.
  (Fhi.Metadata-l9l2n.12)
- **The pager's buttons are never `disabled`** - at the first and last page they carry
  `aria-disabled="true"` and do nothing when pressed. A host stylesheet has to draw the
  unavailable state from that attribute rather than from `:disabled`, or the ends of the list
  look no different from the middle. The reason is focus: pressing Neste until the last page is
  the ordinary way to reach it, and disabling the element that currently has focus drops focus to
  `<body>`, which would leave a keyboard user tabbing from the top of the host's page.
  (Fhi.Metadata-l9l2n.12)
