category: Notes for hosts
- **The variabelutforsker's facet panel emits one new class name,
  `munin-explorer-filters__groupcount`.** It is the count beside a kildetype group in the Kilde
  facet, and it replaces `munin-explorer-filters__chosen` there; `__chosen` itself is unchanged and
  still worn by the kildeutforsker's facet summaries, so a host styling it needs no edit. A handle
  rather than a name that carries meaning — the digits are markup, so an undefined one is still
  read and announced, and what a rule buys is the dimming and `font-variant-numeric: tabular-nums`.
  The tabular figures are the half worth copying: the counts stack, and two of different digit
  widths above one another shift sideways as the facet is narrowed without them. The rule is merged
  on `Fhi.Helsedata.Stiler`'s `main`, added to `__chosen`'s own selector list rather than copied
  beside it, and it is **not in any published Stiler yet** — so every host draws these counts at the
  page's own size and colour today, whatever its pin, and there is no newer pin to move to. Which
  version will first carry it cannot be named from here: Stiler's csproj sits at `0.0.0-local` and
  its pipeline stamps the real `0.1.x` when a release is cut, so the number exists only once that
  happens. The merge is recorded in bead `Fhi.Metadata-l9l2n.73` — its close note names PR 39274,
  merged 2026-09-11 — which is the only record of it this repository can reach; nothing here reads
  Stiler. Both sample stylesheets show the rule. (Fhi.Metadata-l9l2n.104)
