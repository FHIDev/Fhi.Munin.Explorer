category: Notes for hosts
- **Three class names stop being emitted, and one rule you may have written no longer has an
  element.** `munin-explorer-kilde__aside`, `munin-explorer-datasamling__aside` and
  `munin-explorer-whole__aside` are gone from the markup; a rule for any of them now matches
  nothing and can be deleted whenever it suits you. Two consequences are worth knowing rather than
  discovering. Anything scoping `munin-explorer-meta__grid` to one lane inside those asides stops
  applying, so the fact lists take the unscoped two-lane default, which is the right shape for a
  full-width column — do not narrow the base rule to win it back, or the metadata groups lose their
  two lanes with it. And `munin-explorer-kilde__body`, `munin-explorer-datasamling__body` and
  `munin-explorer-whole__body` were grids whose second track is a fixed 320px above 1024px. With
  nothing left to put in it that track was empty space to the right of the page: rendered in
  `samples/LegacyHost` at a 1440px viewport before this change, the main column measured 954px
  inside a 1298px body, the missing 344px being the track and its gap. Do not go and edit those
  three rules, though — `Fhi.Metadata-35w0p.9`, in this same release, stops emitting all three
  names, so the element is `munin-explorer-page__body` and nothing wears them at all. The entry
  under Removed says what to move where. (Fhi.Metadata-35w0p.6)
