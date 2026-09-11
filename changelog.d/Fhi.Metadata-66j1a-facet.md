category: Fixed
- **The variabelgruppe filter panel draws an opted-out group as a container, not a checkbox.**
  `GET /api/explorer/filters` returns a group marked `filter: "2"` in `variabelgrupper`
  nonetheless, when an offered descendant has to nest under it. The panel ticked such a row like
  any other and submitted it, offering a filter the API withholds; it is now drawn as the plain
  label its children hang from, the way `VariabelgruppeFacet.IsStandaloneFacetOption` says to.
  That label is marked with the catalogue's own language like the checkbox labels around it, so an
  English reader hears a Norwegian group name in a Norwegian voice there as well as in its chip.
  A reader who arrives with one already selected — through a shared link, or a host mounting with
  `Filter` set — still has the chip over the results to take it off.
