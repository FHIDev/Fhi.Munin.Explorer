category: Notes for hosts

- **Three class names to style if you are not on Stiler.** The delkilde tree emits
  `munin-explorer-kilde__delkilder` for the list, `munin-explorer-kilde__delkilde` for each item and
  `munin-explorer-kilde__delkilde-name` for the name heading. They are handles rather than names
  carrying meaning nothing else carries: the shape underneath is a real nested `<ul>`/`<li>`, so a
  host that supplies no rule for any of them still gets a list a browser indents by itself and a
  screen reader reads as nested. Both sample hosts' `host.css` carries rules for all three, right
  after the kilde view's own block. The delkilde's code line reuses
  `munin-explorer-kilde__identifiers`, which the kilde's own name block already emits, so there is
  no fourth name to add. (Fhi.Metadata-wtz80)
- **These three are not in Stiler yet.** Nothing in this repository can see
  `Fhi.Helsedata.Stiler` — the CI here checks the sample stylesheet and helsedata's captured class
  names, neither of which is Stiler — so green CI on this change is not evidence the tree is styled
  on helsedata.no. The rule has to land in Stiler under `components/munin-explorer/` the way the
  rest of the prefix did; until it does, a Stiler-only host gets the browser's own list indentation,
  which reads as a plain nested list rather than as nothing. (Fhi.Metadata-wtz80)
- **The datasamling table needs its first column pinned now that there is one table per level.**
  Stiler already pins the third (`24%`) and fourth (`width: 1%` + `nowrap`) and leaves Navn and
  Beskrivelse to auto-layout, which is right for one table — whatever those two settle on is at
  least self-consistent. It is not right for six: auto-layout sizes each table from its own
  content, so Tromsø's first column measured 903, 1426, 270, 1409 and 1479 pixels across five
  tables, and the wave whose beskrivelse holds a wall of text squeezed the rest to slivers. Pinning
  Navn leaves Beskrivelse as the only free column, which lines every level up. Both sample hosts do
  this now; a host writing its own rule wants the same, and so does Stiler.
- **Do not indent the top level of the delkilde list.** The `<ul>` is a SIBLING of the table holding
  the kilde's own datasamlinger, and a rule that indents it claims a parent it does not have: the
  first attempt put the top-level waves 36px in, directly under the last row of that table and with
  no gap, and every reader of the page took Tromsø4 through Tromsø7 to be children of Tromsø3. The
  markup said otherwise, and nobody can see markup. Indentation is spent on depth INSIDE the tree
  only. Both sample hosts draw each delkilde as a bordered box instead, flush with the table at the
  top level, so a nested wave is inset by its parent box's own padding rather than by a rule that
  has to know how deep it is. (Fhi.Metadata-wtz80)
