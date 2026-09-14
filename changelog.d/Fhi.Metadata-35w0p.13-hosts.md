category: Notes for hosts
- **`munin-explorer-page__header` is a new class name, and no published `Fhi.Helsedata.Stiler`
  carries a rule for it.** It is the name block of the saved-list view, which is the fourth surface
  on the detail chassis and the only one with no prefix of its own — the other three wear
  `munin-explorer-kilde__header`, `munin-explorer-datasamling__header` and
  `munin-explorer-whole__header`, and Stiler's rules are keyed on those three names. A handle:
  undefined, the heading is still a heading and still wears Stiler's own `headline headline-s`, so
  what is lost is the separator and the space under the name block that the other three detail pages
  have. Give it what those three declare — 16px of padding below, 16px of margin below, a 1px grey
  rule along the bottom — and both sample stylesheets here stand in at exactly that.
  `Fhi.Metadata-urbj0` is the bead that writes the rule in Stiler. (Fhi.Metadata-35w0p.13)
- **`munin-explorer-page`, `munin-explorer-page__body` and `munin-explorer-page__main` are now worn
  by a fourth view, and this one never draws `munin-explorer-page__toc`.** The saved-list view has no
  contents nav by decision — the Kilde filter beside it does the same grouping job — so its body
  always has exactly one child. **If you write the two-track body rule yourself, gate it on the
  contents column**, as the note under the chassis's own entry said and as both sample stylesheets
  do: `.munin-explorer-page__body:has(> .munin-explorer-page__toc)`. Ungated, a fixed 250px first
  track puts this view's whole list, scroll container and pager in the rail. Stiler 0.1.75 publishes
  that rule ungated, so a host on it draws exactly that until `Fhi.Metadata-ex5wb` lands; the sample
  stylesheets take the track back explicitly for the same reason. (Fhi.Metadata-35w0p.13)
