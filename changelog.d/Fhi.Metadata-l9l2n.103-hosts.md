category: Notes for hosts
- **`munin-explorer-kilder-scroll--cols-N` needs no rule, and that is the point of it.** It is a
  modifier on a box whose base class `munin-explorer-kilder-scroll` you are already styling, so a
  stylesheet that ignores it leaves the box exactly as it renders today — nothing degrades to a raw
  browser default, unlike every other name this package invents. N runs from 4 to 15: four columns
  are always drawn, a fifth appears where the host wired the handover to the variable explorer, and
  the reader turns the other ten on and off from the column picker.
  What a rule buys is the failure it exists for. A host that gives the box `overflow-x: auto` at
  every width needs nothing here. A host that makes it `overflow-x: visible` above a breakpoint —
  which `Fhi.Helsedata.Stiler` does above 780px, so that the page is the sticky ancestor the table's
  header pins to — has a table that runs past its box once the reader turns the wide columns on:
  measured at 1440×900, fifteen columns put the table 663.6px outside the box and the whole host
  page into a horizontal scroll, which is a WCAG 1.4.10 failure on the host's own site. Select on
  the counts that overflow your layout. **Do not answer it by restoring a scroll container above the
  breakpoint** — that takes the sticky ancestor away and the table's header stops pinning, which is
  a worse regression than the spill. (Fhi.Metadata-l9l2n.103)
