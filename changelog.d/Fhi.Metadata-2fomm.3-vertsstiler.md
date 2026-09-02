category: Notes for hosts

- **Two class names to style if the facet panel is to be open on a wide screen.** Kelda's kilde
  list emits `munin-explorer-filters__toggle` for the "Vis filtre" button and
  `munin-explorer-filters__facets` for the panel it unfolds. The fold itself is the browser's own
  `hidden` attribute, so a host that supplies no rule for either still gets a panel that opens and
  closes at every width, and nothing is broken. What it does not get is the sidebar: the filters
  stay folded behind "Vis filtre" on a screen with room to show them outright. What a host has to
  supply is one media query at its own sidebar width holding two declarations,
  `display: none` on `.munin-explorer-filters__toggle` and `display: block` on
  `.munin-explorer-filters__facets[hidden]`. Both or neither: hiding the button while the facets
  stay folded leaves no way to open them at all, which is worse than the fold.
  `Fhi.Helsedata.Stiler` carries the pair at `min-width: 1024px`, the width its own rules move the
  panel into the sidebar at, and both sample hosts' `host.css` carries the same pair. A host on
  neither has to write it. (Fhi.Metadata-2fomm.3)
