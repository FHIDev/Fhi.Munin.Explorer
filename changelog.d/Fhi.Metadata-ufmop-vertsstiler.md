category: Notes for hosts
- **The Dataperiode column now holds a longer string, and the width it is given was measured for
  the shorter one.** A two-ended period reads `1. jan. 1979 – 31. des. 2024` where it read
  `jan 1979 – des 2024`, roughly a third wider. `Fhi.Helsedata.Stiler` gives
  `.munin-explorer-dataitem-main__period` `flex: 150 1 0`, taken from what `jan. 2001 – des. 2025`
  needed without wrapping, and the cell's text is `white-space: normal` — so the string wraps
  rather than overflowing and every result row and saved-list row with two ends grows a line
  taller. Nothing is unreadable and nothing is cut off. The Stiler width is filed as its own work
  item (`Fhi.Metadata-byvcr`); a host outside helsedata's estate that copied that number wants
  roughly 1.3× it, or 1.45× to keep the widest case (`31. mars … – 31. mars …`) on one line.
  (Fhi.Metadata-ufmop)
