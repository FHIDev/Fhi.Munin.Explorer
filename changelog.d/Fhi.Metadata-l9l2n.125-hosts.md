category: Notes for hosts
- **A detail page's root carries `--munin-explorer-stuckbar-height` while the sticky bar is shown.** The name
  munin-explorer-stuckbar-height is a custom property, not a class, so it needs no rule of its own. It is 0px
  or unset otherwise, and removed when the page goes away. A Fhi.Helsedata.Stiler release with the matching
  rule reads it in `.munin-explorer-page__toc`'s `top` and `max-height`; until one is pinned, the property is
  set and nothing reads it, so the page looks as it does today.
