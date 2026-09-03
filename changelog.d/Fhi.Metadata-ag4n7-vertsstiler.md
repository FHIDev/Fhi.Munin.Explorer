category: Notes for hosts
- **`munin-explorer-search` is gone, and `munin-explorer-search__clear` now needs a rule that puts
  it inside the field.** The wrapper existed only to place the clear button beside the search box
  and has no element to name any more; a host styling it can drop those rules. The clear control
  itself moved into `searchbox__freetext-container`, which is the positioned box the search button
  already sits in, so its rule wants `position: absolute` and a `right` offset that clears the
  search button while staying inside the padding the field reserves — in the sample stylesheets
  that is `right: 72px` at `2rem` wide against a 104px reservation. Those numbers are the samples'
  own: a host whose search button is a different width needs its own. A host that defines nothing
  for the name gets the control in normal flow after the field, which is roughly where it stood
  before, so nothing disappears. It carries no visible text — the label is on `aria-label` — so a
  rule that hides it hides the only way to clear the search.
