category: Notes for hosts

- **`munin-explorer-kilder__expand-icon` is new, and a host on `Fhi.Helsedata.Stiler` needs no rule
  for it.** The chevron inside the kilder table's expand button wears Stiler's own `icon`,
  `icon--nomargin` and `icon-keyboard-arrow-right` / `icon-keyboard-arrow-down` beside it, and those
  are what draw it: `.icon` is `1.5rem` square, which is the whole of why the button now measures at
  least 24 x 24. The new name is a handle for a host or a test to find it by. A host **without**
  Stiler owes it that box itself — under 24 x 24 the control fails WCAG 2.5.8 — and both sample
  stylesheets show the shape it needs. (Fhi.Metadata-mpx2p)
