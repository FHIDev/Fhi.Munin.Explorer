category: Fixed
- **The kilde page's contents list no longer slides under the sticky name bar.** The browser module
  writes the shown bar's height, 77px or 126px as the name wraps, to `--munin-explorer-stuckbar-height`
  on the page root, and follows it as the bar resizes; Fhi.Helsedata.Stiler's contents rule clears it.
