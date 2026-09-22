category: Notes for hosts
- **Nested criteria retain fragment navigation clearance.** Style `munin-explorer-page__anchor`
  with the same responsive `scroll-margin-top` as main detail sections (140px desktop,
  80px through 767px in Stiler). This target deliberately has no `data-nav-section`, so a
  criteria link clears sticky headers without adding a contents entry. Stiler support is
  supplied by Stiler PR 39519 (`Fhi.Metadata-17k34`); Stiler 0.1.91 does not yet contain this rule.
