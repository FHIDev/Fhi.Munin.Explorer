category: Changed
- **The filter tree's level guides are on when the panel first renders.** `LevelLines` now
  defaults to `true`, so `data-level-lines="true"` is on `munin-explorer-filters` from the first
  paint and `Nivålinjer` turns the guides off rather than on. Runa's own tree loads with its
  toggle pressed, and a reader who never finds the button was reading a deep tree with no guides
  at all. A host that stores what `LevelLinesChanged` raises is unaffected — whatever it passes
  back still wins; a host that stores nothing gets the guides at every visit. Pass
  `LevelLines="false"` to keep the previous state. (Fhi.Metadata-dfygj)
