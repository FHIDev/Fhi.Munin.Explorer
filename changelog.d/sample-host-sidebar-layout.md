category: Notes for hosts

- **The sample hosts now show the filter panel as a sidebar, and record what that costs** - the
  filters used to stack above the results, which meant the sample opened on four thousand pixels
  of facets with the first result below all of them. The layout is Runa's, measured off it: a
  384px filter column, a 24px gutter, and scrolling that starts only above 1024px. Nothing in the
  package changed — the component already put the filter panel and the results list as siblings
  under one root, so a host reaches this with a grid rule and no markup change. Three details are
  worth copying rather than rediscovering: the panel is a `<fieldset>` and so needs
  `min-inline-size: 0` before it will shrink into a column at all; it needs to span every results
  row, with `span 99` rather than `-1`, because the results rows are implicit; and Stiler's
  buttons are `white-space: nowrap`, so a facet named "Nasjonalt kvalitetsregister for ..." asks
  for 565px in a 384px column until the label is allowed to wrap.
