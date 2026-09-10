category: Notes for hosts

- **Delete any rule you wrote for `munin-explorer-selection__explore`; it is no longer emitted.**
  A previous note here asked hosts for a `min-width` on it, so that the handover button held still
  across its three labels. `Fhi.Helsedata.Stiler` measured that floor and declined to ship one: the
  widest label is English's, and a floor wide enough for it leaves around 104px of dead button in
  the common Norwegian state. Nothing intends to hold the width now, so the button sizes to its own
  label and the *Nullstill utvalg* button and the count slide when it changes — accepted rather
  than overlooked. The button itself is unchanged; `hd-button-square button-square--primary` is
  what always drew it. (Fhi.Metadata-kvgu7)

- **The floor went, but the handover still has to be allowed to wrap.** `hd-button-square` is
  `white-space: nowrap` at a fixed `2.75rem`, so the widest label is one unbreakable line: measured
  on the sample host at a 320px viewport it draws 306px wide in a 226px row and scrolls the page
  sideways, which is WCAG 1.4.10 Reflow. Give the ribbon's button `white-space: normal`,
  `height: auto` and a `min-height: 2.75rem` floor, on a selector that outranks the bare
  `hd-button-square` — both sample stylesheets scope it
  `.munin-explorer .munin-explorer-selection .hd-button-square`, three classes, so source order
  cannot take it back. Those three are the whole of what a host needs. The removed rule carried
  six, and the other three are gone deliberately: `min-width: min(21rem, 100%)` is the floor Stiler
  declined; `justify-content: center` is already declared by Stiler's own `.hd-button-square`, so
  repeating it drew nothing; and `max-width: 100%` was there to cap that floor, and with no floor
  left the ribbon's flex row is what keeps the button inside the line — the 320px measurement above
  is with the three and without it. `Fhi.Metadata-s4es0` asks `Fhi.Helsedata.Stiler` for the same
  three, and until that ships a host with no rule of its own overflows. (Fhi.Metadata-kvgu7)

- **`munin-explorer-selection`, the ribbon around it, stays and still needs `display: flex`.** A
  host that draws nothing for it gets the handover, the reset and the count stacked. Stiler has the
  rule on `main` and it is **not** in 0.1.42, the version helsedata.no pins today, so a host on that
  pin still supplies it themselves until the pin moves (`Fhi.Metadata-kpmt3`). Both sample
  stylesheets show what it wants. (Fhi.Metadata-kvgu7)
