category: Changed
- **The kildeutforsker's result count says how many of the catalogue you are looking at, and how
  many filters are narrowing it.** The line over the list used to say only the total it was
  currently showing, so a narrowed list read exactly like a short catalogue and nothing on the page
  said filtering was happening at all. It now reads `56 kilder av 66 — 2 filtre aktive` in
  Norwegian and `56 sources of 66 — 2 filters active` in English. Both clauses are absent rather
  than zeroed on an untouched list, which still reads `66 kilder`: `66 kilder av 66 — 0 filtre
  aktive` is more words saying less. The filter count is the same number the empty state already
  reports, so a narrowed list and a list narrowed to nothing cannot name different filters. The
  sentence is both the polite status line and the table's accessible name, as before, so a screen
  reader hears the change too. No new class name and nothing new for a host to style — the three
  controls above the table still take a row each, which needs a Stiler rule and is tracked
  separately. (Fhi.Metadata-l9l2n.54)
