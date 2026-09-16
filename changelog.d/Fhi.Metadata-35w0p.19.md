category: Changed
- **A detail page's sections are identified and ordered by the catalogue rather than by its own
  data.** Two properties are in the same section when they carry the same `groupKey`, so renaming a
  section's title in one language no longer splits it in two; and the sections are ordered by
  `groupSortOrder`, so filling in a previously-empty property no longer moves a section up the page.
  Both are fallbacks rather than requirements — a payload carrying neither is grouped by its
  headings and ordered by its members exactly as before, so upgrading this package never requires an
  API of a particular age. A property the payload files under no section is still drawn nowhere,
  which is unchanged: those are the column-backed keys each detail view already draws itself.
  (Fhi.Metadata-35w0p.19)
