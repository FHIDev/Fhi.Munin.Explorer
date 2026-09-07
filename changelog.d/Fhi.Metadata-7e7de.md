category: Notes for hosts

- **The explorer's root element carries its own width, and the drill-in spans the whole grid.**
  `Fhi.Helsedata.Stiler` gives `.munin-explorer` a base rule of its own — `max-width: 1488px;
  width: 100%; padding: 0 24px; margin: 0 auto` — because the component is a `<section>` a host
  drops straight into the page and nothing above it bounds the width; and it lifts
  `.munin-explorer-drilldown` to `grid-column: 1 / -1`, excluding it from the catch-all that places
  everything else in the results column. A host laying the component out in two columns needs both.
  Without the first the component runs to the window edge; without the second the drill-in renders
  in the results column with the filter track standing empty — measured in the sample at 408px of
  dead gutter, and 1041px of content in a 1024px viewport. (Fhi.Metadata-7e7de)
