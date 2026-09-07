category: Notes for hosts

- **`munin-explorer-meta__grid-1` carries a placement rule as well as a track count, and
  `munin-explorer-meta__grid-2` only gets its one lane if the override is ordered after the base
  rule.** In `Fhi.Helsedata.Stiler` the two modifiers share `grid-template-columns: auto` and
  `-1` additionally has `grid-row: 1/3`, both declared after `.munin-explorer-meta__grid`'s own
  `1fr 1fr`. A host writing its own stylesheet needs the same two things: the placement rule, which
  is inert wherever the element's parent is not a grid container and stops being inert the moment
  it is, and the source order, because the modifier and the base rule are equally specific and the
  later one wins. The sample stylesheets had neither, so `-1` lost its row span and `-2` silently
  kept two lanes. (Fhi.Metadata-l9txl)
