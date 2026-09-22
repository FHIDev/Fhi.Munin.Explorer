category: Fixed
- **The chevron that opens a kilde row or a variable row now emits the icon class of the glyph it
  actually draws.** Collapsed is `icon-keyboard-arrow-down` and expanded is
  `icon-keyboard-arrow-up`, on both `KildeExplorer`'s kilde table and `VariableExplorer`'s result
  rows. Nothing moves on screen: the collapsed chevron pointed down and the expanded one up
  before this too. What changes is that the markup says so — the collapsed state used to emit
  `icon-keyboard-arrow-right` and rely on `Fhi.Helsedata.Stiler` mapping that name to the
  downward glyph, so a reader of the markup and a reader of the screen disagreed and neither
  could tell which was wrong without opening the other repository. The chevron keeps its
  `munin-explorer-kilder__expand-icon` / `munin-explorer-dataitem-main__expand-icon` class and its
  `aria-expanded`, and no `munin-explorer*` name is added or renamed. (Fhi.Metadata-l9l2n.84)
