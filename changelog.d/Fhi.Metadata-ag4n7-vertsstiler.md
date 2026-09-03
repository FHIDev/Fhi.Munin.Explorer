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
- **It still needs a muted `[aria-disabled="true"]` appearance, in the variable explorer.** The
  control is drawn whenever the box has a term, but the variable explorer refuses the press while
  its own search is in flight, so there is a window where it is on screen and will not act. It
  says so with `aria-disabled` rather than `disabled`, which would drop the focus this control was
  moved inside the field to keep — so it stays focusable and hoverable, and both states need to
  stop looking like invitations. The kildeutforsker fetches nothing and never carries the
  attribute.
