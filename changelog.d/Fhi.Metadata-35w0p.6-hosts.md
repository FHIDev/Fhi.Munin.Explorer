category: Notes for hosts
- **Three class names stop being emitted, and one rule you may have written no longer has an
  element.** `munin-explorer-kilde__aside`, `munin-explorer-datasamling__aside` and
  `munin-explorer-whole__aside` are gone from the markup; a rule for any of them now matches
  nothing and can be deleted whenever it suits you. Two consequences are worth knowing rather than
  discovering. Anything scoping `munin-explorer-meta__grid` to one lane inside those asides stops
  applying, so the fact lists take the unscoped two-lane default, which is the right shape for a
  full-width column — do not narrow the base rule to win it back, or the metadata groups lose their
  two lanes with it. And `munin-explorer-kilde__body`, `munin-explorer-datasamling__body` and
  `munin-explorer-whole__body` were grids whose second track is a fixed 320px above 1024px. With
  nothing left to put in it that track is empty space to the right of the page: rendered in
  `samples/LegacyHost` at a 1440px viewport before this change, the main column measured 954px
  inside a 1298px body, the missing 344px being the track and its gap. **Both sample stylesheets
  now declare one track**, so a host that copied them should take the same line out. The
  `Fhi.Helsedata.Stiler` rule they stand in for still declares two, and `Fhi.Metadata-35w0p.39`
  is what removes it — a separate release, so that the two need not land together. Until it ships,
  a host on Stiler alone draws the empty track; a host writing its own rules for those three names
  can drop the second one today. (Fhi.Metadata-35w0p.6)
