category: Notes for hosts
- **The variabelutforsker's facet panel emits one new class name,
  `munin-explorer-filters__groupcount`.** It is the count beside a kildetype group in the Kilde
  facet, and it replaces `munin-explorer-filters__chosen` there; `__chosen` itself is unchanged and
  still worn by the kildeutforsker's facet summaries, so a host styling it needs no edit. A handle
  rather than a name that carries meaning — the digits are markup, so an undefined one is still
  read and announced, and what a rule buys is the dimming and `font-variant-numeric: tabular-nums`.
  The tabular figures are the half worth copying: the counts stack, and two of different digit
  widths above one another shift sideways as the facet is narrowed without them. The rule reached
  `Fhi.Helsedata.Stiler`'s `main` in PR 39274 on 2026-09-11, added to `__chosen`'s own selector
  list, so a host on a Stiler published before that merge draws these counts at the page's own size
  and colour until it moves its pin. Both sample stylesheets show the rule.
  (Fhi.Metadata-l9l2n.104)
