category: Notes for hosts
- **The pager needs three class names that are not in Stiler** - `variables-pagination`,
  `variables-pagination-content` and `skiplink-pagination`. Read back off the compiled
  stylesheets: Stiler has no pagination rule of any kind — no `pagination`, `pager`, `paging`,
  `page-link` or `page-item` — while helsedata's own variable page styles its pager from a
  page-specific `variables.css` that the site-wide stylesheet does not carry. The component emits
  helsedata's names rather than inventing its own, which is the standing decision about whose
  clothes this component wears, so mounting it on that page needs nothing new. Mounted anywhere
  else — and where it will be mounted is not settled yet — the host has to supply those three
  itself, including the rule that keeps `skiplink-pagination` out of sight until it is focused.
  Both sample hosts show a working approximation. (Fhi.Metadata-l9l2n.12)
- **The pager's buttons are never `disabled`** - at the first and last page they carry
  `aria-disabled="true"` and do nothing when pressed. A host stylesheet has to draw the
  unavailable state from that attribute rather than from `:disabled`, or the ends of the list
  look no different from the middle. The reason is focus: pressing Neste until the last page is
  the ordinary way to reach it, and disabling the element that currently has focus drops focus to
  `<body>`, which would leave a keyboard user tabbing from the top of the host's page.
  (Fhi.Metadata-l9l2n.12)
