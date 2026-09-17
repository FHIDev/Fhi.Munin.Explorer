category: Notes for hosts
- **`munin-explorer-breadcrumb` now dresses a second trail, and needs no new rule.** The
  Plassering trail on the whole-variable page is wrapped in it, so the rule a host already has for
  the trail over the search results is what strips the list markers and draws the chevrons there
  too. `Fhi.Helsedata.Stiler` carries `.munin-explorer-breadcrumb`, `… ol` and `… li + li::before`
  unscoped, declaring what both sample stylesheets declare — which is why
  `test/sample-css-known-divergences.txt` has no line for any of the three. A host that draws none
  of it gets a numbered list of the same steps in the same order. Unlike the results trail, this
  wrapper carries no `role="navigation"`: nothing in it is pressable. (Fhi.Metadata-35w0p.47)
