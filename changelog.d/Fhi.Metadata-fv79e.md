category: Notes for hosts

- **`munin-explorer-meta__grid` needs a wrapping rule, and no Stiler version has carried one.** A
  grid track floors at min-content, so a single unbreakable value sizes the whole column: "Gjeldende
  lovgivning" arrives as two ELI-URLs joined with semicolons, one 586px token, and every cell in
  that column reached 17px past the viewport at 1024px — the page gained the scrollbar, not the
  panel. Above 1280px it hid rather than went away, as a lopsided 306px + 105px pair where the two
  tracks should be equal. `overflow-wrap: anywhere` on the grid closes it; `minmax(0, 1fr)` on the
  tracks does not, because below 1280px the single-track rule already overrides them and above it
  the token spills out of a box that has stopped growing — the page still scrolls while every
  element measures inside the viewport. Counted in the published `Fhi.Helsedata.Stiler` 0.1.39: 37
  `overflow-wrap`/`word-break` declarations in the stylesheet, none of them in a selector naming
  `munin-explorer`. Filed there as Stiler PR 39156; the sample stylesheets here mirror it.
  (Fhi.Metadata-fv79e)
