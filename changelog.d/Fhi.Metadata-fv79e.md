category: Notes for hosts

- **Give `munin-explorer-meta__grid` `overflow-wrap: anywhere`, unconditionally.** No published
  `Fhi.Helsedata.Stiler` carries it — 0.1.39 has 37 `overflow-wrap`/`word-break` declarations and
  none of them in a selector naming `munin-explorer` — so a host on Stiler today has this defect;
  it is proposed there as PR 39156 and is mirrored in the sample stylesheets here. Without it a
  grid track floors at min-content, so a single long value sizes the whole column: "Gjeldende
  lovgivning" arrives as ELI-URLs joined with semicolons, measuring 586px inside a 435px grid, and
  the page — not the panel — gains a horizontal scrollbar. `overflow-wrap: break-word` does **not**
  substitute: it leaves min-content alone, so the track stays floored. Neither does `minmax(0, 1fr)`
  on the tracks, since a single-track rule already overrides them below 1280px and above it the
  value spills out of a box that has stopped growing — the page still scrolls while every element
  measures inside the viewport.
- **It costs mid-word breaks, and that is the trade rather than an oversight.** Letting the track
  shrink is the whole mechanism, so Norwegian prose in a narrow column breaks inside words:
  measured on the sample host, 13 such breaks at 1024px, 4 at 1281px, 1 at 1718px, against 0 today.
  A host that would rather keep whole words has to accept the sideways scroll instead; there is no
  third option that fixes one without the other. (Fhi.Metadata-fv79e)
