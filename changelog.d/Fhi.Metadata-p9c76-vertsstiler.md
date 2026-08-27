category: Notes for hosts
- **The retry buttons need a rule for `munin-explorer-retry`.** Their enabled look is
  `hd-button-square button-square--ghost`, which `Fhi.Helsedata.Stiler` already defines; their
  inert look is not covered by anything it ships. They are never `disabled` — that would drop
  focus to `<body>` at the moment they stop being useful — so `aria-disabled` is what says so, and
  the pager and the filter panel both draw that state from rules scoped to their own containers.
  The alert region these sit in deliberately carries no class, so neither rule reaches in, and
  without one of its own a button that does nothing looks exactly like one that works — which is a
  WCAG 2.1 AA problem rather than a cosmetic one. Both sample hosts' `host.css` carries the rule;
  Stiler needs the same under `components/munin-explorer/`. (Fhi.Metadata-p9c76)
