category: Notes for hosts

- **New class name: `munin-explorer-filters__count`**, on the number beside every facet value in
  both the kilde explorer and the variable explorer. A host that defines no rule for it loses
  nothing it had before — the count renders inline, exactly as it did when it was part of the
  label's text. A rule is what buys the dimming and the tabular alignment that keep a column of
  numbers from reading as more of the words in front of them; the sample stylesheets show one.
- **Do not move the count out of its `<label>`.** The label is what names the facet checkbox, so a
  rule that lifts the count out of it — `position: absolute`, a flex `order` that reparents, or
  `display: contents` on the label — takes the number out of what a screen reader announces for
  that value. Dimming, alignment and spacing are all safe; moving it out is not.
  (Fhi.Metadata-cgk85)
