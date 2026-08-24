category: Notes for hosts
- **The pager and its skip link both wear our own class names, and Stiler carries their rules from
  0.1.14** - `munin-explorer-pagination*` and `munin-explorer-skiplink-pagination`. Neither was in
  Stiler to begin with: it has no pagination rule of its own — no `pagination`, `pager`, `paging`,
  `page-link` or `page-item` — while helsedata's own variable page styles both from a
  `variables.css` the site-wide stylesheet does not carry. The skip link's rule is the one to
  supply first on an older Stiler, because it is what keeps the link out of sight until it is
  focused rather than what gives it a look. Both sample hosts show a working approximation.
  (Fhi.Metadata-l9l2n.12)
- **The pager's buttons are never `disabled`** - at the first and last page they carry
  `aria-disabled="true"` and do nothing when pressed. A host stylesheet has to draw the
  unavailable state from that attribute rather than from `:disabled`, or the ends of the list
  look no different from the middle. The reason is focus: pressing Neste until the last page is
  the ordinary way to reach it, and disabling the element that currently has focus drops focus to
  `<body>`, which would leave a keyboard user tabbing from the top of the host's page.
  (Fhi.Metadata-l9l2n.12)
