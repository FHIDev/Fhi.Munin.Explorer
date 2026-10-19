category: Notes for hosts
- **A detail page's root now carries `--munin-explorer-stuckbar-height` while the sticky bar is shown.** It is
  0px otherwise and removed when the page goes away. Fhi.Helsedata.Stiler reads it in
  `.munin-explorer-page__toc`'s `top` and `max-height`; until a Stiler release with that rule is pinned, the
  property is set and nothing reads it, so the page looks as it does today.
