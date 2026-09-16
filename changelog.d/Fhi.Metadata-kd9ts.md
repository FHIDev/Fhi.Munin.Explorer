category: Added
- **The variable explorer's filter panel gains an Ikoner switch beside Nivålinjer, and
  `VariableSearch` a two-way `ShowNodeIcons` to go with it.** It turns off the node icons the
  panel's kilde tree draws in front of each name — the folder on a kilde or a delkilde, one glyph
  per datakategori on a datasamling — and it reaches the kilde a reader drills into as well, so one
  press decides both surfaces rather than leaving the pictures waiting one click away. The same
  parameter name and the same on-by-default meaning `KildeView` and `KildeHierarchyView` already
  carry, so a host setting it on more than one of them sets one thing.
  Off costs every reader the same thing. The glyphs are `aria-hidden` and the `screenreader-only`
  words beside them are those same categories said aloud, so the switch takes both or neither: a
  datasamling stops naming its datakategorier on the row whether the reader is looking at the tree
  or listening to it. Keeping the words behind an `Ikoner` switch that is off would leave a control
  that does nothing at all for the reader pressing it, which is why `KildeHierarchyView`'s own
  parameter has always taken both. Nothing else on the row moves: the filter it ticks, the counts
  beside it, the level lines and the kildetype badge on a kilde are drawn exactly as before, and
  the badge in particular is real text outside the icon slot, so it stays in the checkbox's
  accessible name whichever way the switch is set.
  It is a native `<button role="switch">` carrying `aria-checked`, the same control Nivålinjer is,
  so it announces as on and off and answers Enter and Space with no key handler of this package's
  own.
  `ShowNodeIconsChanged` is what a host stores. Like `LevelLines` beside it the parameter is read
  once at mount and owned by the component afterwards — the package makes no JS interop call and so
  reaches no `localStorage`, and what is remembered about a reader is the host's own policy to set
  — so a host that wants the choice to survive a visit stores what the callback raises and supplies
  it at the next mount. A host that stores nothing gets the icons at every visit. No new class name
  is added: the switch wears the pair Nivålinjer already wears.
  (Fhi.Metadata-kd9ts)
