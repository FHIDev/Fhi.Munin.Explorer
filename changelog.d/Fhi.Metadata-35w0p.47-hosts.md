category: Notes for hosts
- **`munin-explorer-breadcrumb` now dresses a second trail.** The Plassering trail on the
  whole-variable page is wrapped in it, so the rule a host already has for the trail over the
  search results is what strips the list markers and draws the chevrons there too. No new class
  name, and both sample stylesheets already carry the rules unchanged. `Fhi.Helsedata.Stiler`
  declares the name — the divergence baseline reports no unstyled name for it — but whether its
  breadcrumb partial includes the descendant `ol` and `li + li::before` rules could not be read
  from this repository, and `Fhi.Metadata-40y6v` is open to settle it against a checkout. Until it
  does, a Stiler host may draw these steps as a numbered list rather than a chevron-separated
  path: the same steps in the same order, in the wrong shape. (Fhi.Metadata-35w0p.47)
