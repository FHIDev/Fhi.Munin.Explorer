category: Notes for hosts
- **The detail views emit one new class name, `munin-explorer-page__section`, and nine bare element
  ids.** The class is a handle in the plainest sense: the wrapper it dresses has no padding, border
  or margin, so a host that defines nothing for it sees exactly the page it saw before. What a rule
  buys is `scroll-margin`, so a heading reached by fragment clears a sticky header instead of
  landing under it. `Fhi.Helsedata.Stiler` has the rules already — a new
  `components/munin-explorer/_page.scss`, merged as PR 39299 on 2026-09-11, which hangs the offset
  on the `data-nav-section` attribute the same elements carry — but that PR bumped no version, so
  **no published Stiler carries them** and every host, whatever its pin, draws the wrapper undrawn
  today. That costs nothing until the contents nav that uses it arrives. Both sample stylesheets
  show the offset at Stiler's own 140px. The ids are the part to read twice: `metadata`, `criteria`,
  `source`, `statistics`, `datacollections`, `versions`, `dataperiod`, `datatype` and
  `variablegroups`, and they are written **bare**, without the per-instance discriminator every
  other id this package writes carries — a deep link has to mean the same thing in the next reader's
  browser, and an id minted at run time cannot. Within one mount they cannot repeat, since an
  explorer renders at most one detail view. A page that mounts two explorers, or that already means
  something of its own by `id="source"`, will have duplicates: if that is your page, keep your own
  ids off those nine until `Fhi.Metadata-uobxg` settles it. (Fhi.Metadata-35w0p.5)
