category: Removed

- **The kildeutforsker's handover button no longer carries a class of its own.** It is
  `hd-button-square button-square--primary` and nothing else; the
  `munin-explorer-selection__explore` it also wore is gone. No stylesheet anywhere defined that
  name — not `Fhi.Helsedata.Stiler` 0.1.38, not 0.1.42, and not the live helsedata.no bundle — so
  it read as a styling seam and was not one. `munin-explorer-selection`, the ribbon around it,
  stays and is unchanged. The markup is all that changed: the wrap, the auto height and the height
  floor the removed name carried in the sample stylesheets moved to
  `.munin-explorer .munin-explorer-selection .hd-button-square`, so the sample hosts draw the button
  as they did at every width. The three declarations left behind are the `min-width` floor Stiler
  declined, the `max-width` that existed only to cap it, and a `justify-content` Stiler's own
  `.hd-button-square` already declares. A host on `Fhi.Helsedata.Stiler` alone still needs the
  three moved ones until `Fhi.Metadata-s4es0` puts them there. (Fhi.Metadata-kvgu7)
