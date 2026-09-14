category: Changed
- **A sidebar box whose every fact is blank no longer draws its heading.** `Kildeinformasjon` in
  all three detail views, and `Statistikk` in the kilde view, were written unconditionally: the
  `<dl>` under them was suppressed when the catalogue had filled none of the fields in, so a sparse
  record drew a heading with nothing beneath it. They are now written only when there is at least
  one row, which is what wrapping each block in a `<section>` forced the question — a section
  holding a heading and no content is worse than the dangling heading was. The other blocks are
  unaffected: they already answered the emptiness question this way. A host that has written a rule
  against one of those headings being present should know it can now be absent.
  (Fhi.Metadata-35w0p.5)
