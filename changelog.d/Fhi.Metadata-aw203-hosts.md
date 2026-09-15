category: Notes for hosts
- **One class name to style on the variable explorer's Kilde facet.**
  `munin-explorer-filters__badge` is the `<span>` holding the Biobank or Prøvesamling word on a
  kilde row. A handle: the word is real text inside the `<label>`, so with no rule at all it is
  visible and part of the checkbox's accessible name, and what a rule buys is the capsule that
  tells it from the name beside it. Both sample stylesheets already carry the rule to copy, stood
  in from the pinned published Stiler; which release first supplies it is `Fhi.Metadata-gegtb`.
  The folder glyphs the same facet's kilde and delkilde rows now draw need no new name — they wear
  the `munin-explorer-filters__icons` and `munin-explorer-filters__icon` pair a datasamling's
  datakategori glyphs already wear, which `Fhi.Helsedata.Stiler` **0.1.75 or later** supplies. The
  badge is deliberately outside that slot, so a rule — or a future icon toggle — that hides the
  decoration must not hide the badge with it.
  (Fhi.Metadata-aw203)
