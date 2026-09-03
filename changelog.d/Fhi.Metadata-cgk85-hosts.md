category: Notes for hosts

- **New class name: `munin-explorer-filters__count`**, on the number beside every facet value in
  both the kilde explorer and the variable explorer. A host that defines no rule for it loses
  nothing it had before — the count renders inline, exactly as it did when it was part of the
  label's text. A rule is what buys the dimming and the tabular alignment that keep a column of
  numbers from reading as more of the words in front of them; the sample stylesheets show one.
- **Style the count freely, but do not hide it.** The label names the facet checkbox, so the count
  is announced with the value it counts. No layout rule undoes that: `position: absolute`, a flex
  `order` and `display: contents` on the label all leave the announced name at `Aktiv (3)`, because
  CSS changes where a box is drawn, not what the label contains. `display: none` and
  `visibility: hidden` on the count do drop it from that name — a host that hides the number hides
  it from screen readers with it. Dimming, alignment, spacing and repositioning are all safe.
  (Fhi.Metadata-cgk85)
