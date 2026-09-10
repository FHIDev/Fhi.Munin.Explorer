category: Notes for hosts

- **Delete any rule you wrote for `munin-explorer-selection__explore`; it is no longer emitted.**
  A previous note here asked hosts for a `min-width` on it, so that the handover button held still
  across its three labels. `Fhi.Helsedata.Stiler` measured that floor and declined to ship one: the
  widest label is English's, and a floor wide enough for it leaves around 104px of dead button in
  the common Norwegian state. Nothing intends to hold the width now, so the button sizes to its own
  label and the *Nullstill utvalg* button and the count slide when it changes — accepted rather
  than overlooked. The button itself is unchanged; `hd-button-square button-square--primary` is
  what always drew it. (Fhi.Metadata-kvgu7)

- **`munin-explorer-selection`, the ribbon around it, stays and still needs `display: flex`.** A
  host that draws nothing for it gets the handover, the reset and the count stacked. Stiler has the
  rule on `main` and it is **not** in 0.1.42, the version helsedata.no pins today, so a host on that
  pin still supplies it themselves until the pin moves (`Fhi.Metadata-kpmt3`). Both sample
  stylesheets show what it wants. (Fhi.Metadata-kvgu7)
