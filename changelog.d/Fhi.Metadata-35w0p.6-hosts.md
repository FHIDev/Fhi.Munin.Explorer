category: Notes for hosts
- **Three class names stop being emitted, and one rule you may have written no longer has an
  element.** `munin-explorer-kilde__aside`, `munin-explorer-datasamling__aside` and
  `munin-explorer-whole__aside` are gone from the markup; a rule for any of them now matches
  nothing and can be deleted whenever it suits you. Two consequences are worth knowing rather than
  discovering. Anything scoping `munin-explorer-meta__grid` to one lane inside those asides stops
  applying, so the fact lists take the unscoped two-lane default, which is the right shape for a
  full-width column — do not narrow the base rule to win it back, or the metadata groups lose their
  two lanes with it. And `munin-explorer-kilde__body`, `munin-explorer-datasamling__body` and
  `munin-explorer-whole__body` are still grids with a second 320px track above 1024px — in
  `Fhi.Helsedata.Stiler`'s `_trail.scss` and in both sample stylesheets alike: with nothing left to
  put in it, that track is empty space to the right of the page until the Stiler rules are removed
  under `Fhi.Metadata-35w0p.39`. Measured in LegacyHost at a 1440px viewport, the main column is
  954px where the body is 1298px wide. A host writing its own rules for those three names can take
  the second track out today. (Fhi.Metadata-35w0p.6)
