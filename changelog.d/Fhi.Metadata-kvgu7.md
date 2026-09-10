category: Removed

- **The kildeutforsker's handover button no longer carries a class of its own.** It is
  `hd-button-square button-square--primary` and nothing else; the
  `munin-explorer-selection__explore` it also wore is gone. No stylesheet anywhere defined that
  name — not `Fhi.Helsedata.Stiler` 0.1.38, not 0.1.42, and not the live helsedata.no bundle — so
  it read as a styling seam and was not one. `munin-explorer-selection`, the ribbon around it,
  stays and is unchanged. The markup is all that changed: the wrap and height the removed name
  carried in the sample stylesheets moved to
  `.munin-explorer .munin-explorer-selection .hd-button-square`, so the button draws as it did at
  every width, and only the `min-width` floor Stiler declined is actually gone. (Fhi.Metadata-kvgu7)
