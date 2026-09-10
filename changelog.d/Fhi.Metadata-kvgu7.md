category: Removed

- **The kildeutforsker's handover button no longer carries a class of its own.** It is
  `hd-button-square button-square--primary` and nothing else; the
  `munin-explorer-selection__explore` it also wore is gone. No stylesheet anywhere defined that
  name — not `Fhi.Helsedata.Stiler` 0.1.38, not 0.1.42, and not the live helsedata.no bundle — so
  it read as a styling seam and was not one. The button draws exactly as it did, because the two
  Stiler classes beside it were always what dressed it. `munin-explorer-selection`, the ribbon
  around it, stays and is unchanged. (Fhi.Metadata-kvgu7)
