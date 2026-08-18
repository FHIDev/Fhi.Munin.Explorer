category: Notes for hosts
- The sort control adds five class names to the list a host outside helsedata's estate has to
  provide: `form-fieldset`, `button-square--secondary` and `button-square--ghost` (the two states
  of a sort button, alongside the `hd-button-square` base the Søk button already needed), and
  Stiler's `margin-right` / `margin-bottom` modifiers, which only apply on a square button. All
  five were read back off helsedata.no's compiled stylesheet, not off a list of names — Stiler's
  own sort-header rules are scoped under `article.registerOwnerListPage` and are unreachable from
  an embedded component, which is why the ordering is buttons above the list rather than clickable
  column headers. Both sample hosts show a working approximation.
