category: Notes for hosts
- **The variabelutforsker now emits four class names that only the kildeutforsker emitted before,
  and their rules are not in `Fhi.Helsedata.Stiler` 0.1.42.** `munin-explorer-filters__active`,
  `munin-explorer-filters__chip` and `munin-explorer-filters__chip-remove` are the active-filter row,
  the capsule around one chosen value and the close control inside it; `munin-explorer-results__toolbar`
  is the row the result count shares with the Kolonner picker. No name here is new to the package and
  no new rule is needed for this change — a host already styling the kildeutforsker's chip row and
  result row is done. What is new is that the variabelutforsker draws them too, so a host on 0.1.42,
  the version pinned here, now has two surfaces in the undressed case rather than one: the floor is
  the Stiler release of 2026-09-11, the first that follows 0.1.42, and it carries all four. Undressed
  is undressed rather than broken — the chips fall back to inline flow with every word and control
  intact, and the count and picker to two blocks in ordinary flow — except for the close control's
  24×24 box, which is a WCAG 2.5.5 target and is the one thing lost rather than merely undrawn. Both
  sample stylesheets show the shape. (Fhi.Metadata-l9l2n.68)
