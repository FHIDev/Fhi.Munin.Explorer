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
- **The datasamling table needs a fixed column grid now that there is one per level.** Under CSS's
  default `table-layout: auto` each level sizes its own columns from its own content, so Tromsø drew
  five tables whose first column measured 903, 1426, 270, 1409 and 1479 pixels — nothing lining up
  down the page, and the one wave whose beskrivelse holds a wall of text squeezed its other three
  columns to slivers. One flat table hid this, having one column grid however lopsided its content.
  Both sample hosts' `host.css` now sets `table-layout: fixed` on
  `munin-explorer-kilde__datasamlinger` with four percentage widths and `overflow-wrap: anywhere`;
  a host writing its own rule for that class wants the same. (Fhi.Metadata-wtz80)
