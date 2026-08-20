category: Notes for hosts
- The column picker adds eight class names a host outside helsedata's estate has to provide, all
  eight helsedata's own, from the `variables.css` their variable page carries —
  `variable-explorer-header` with `__actions` and `__actions-button`, the bare `dropdown` and
  `variable-explorer__dropdown` together on the disclosure, and `dropdown-choicepicker` with
  `--right` and `__item`. The two on the disclosure do different jobs and both are theirs:
  `.variable-explorer-header__actions .dropdown { width: 100% }` is what widens the trigger to its
  row, and `.variable-explorer__dropdown { z-index: 99 }` is what lifts the open list over the rows
  below it. All of them were read back off the compiled stylesheets rather than off a list of names,
  and each toggle's label is the button's own text so that no ninth name is needed to style it.
  `sortable-dropdown` is deliberately *not* among them, although it looks like the obvious fit: it
  is helsedata's mobile sort control, `display: none` above 1280px, so a picker wearing it would be
  invisible on every desktop.
- The open list is `position: absolute`, and the wrapper carries an inline `position: relative` so
  it anchors to the picker rather than to whatever the host page happens to have positioned above
  it. That is what helsedata's own markup does inline too. A host that styles none of these names
  still gets a working picker — it is a `<details>`, a `<ul>` and buttons in two states, the same
  three shapes the filter panel leans on — drawn in the flow instead of over the list.
- The picker's trigger is a `<summary>` dressed as the ghost square button, and a `<summary>` is
  `display: list-item` by default, so a host owes it two rules —
  `.variable-explorer__dropdown > summary { list-style: none }` and
  `.variable-explorer__dropdown > summary::-webkit-details-marker { display: none }`. Without them
  the button draws a stray browser disclosure triangle beside "Kolonner" that their own button does
  not have. helsedata's own control is a `<button>`, so nothing in their `variables.css` has a
  reason to suppress a marker here: this pair is owed by the primary host as well as by hosts
  outside their estate. Both sample hosts carry exactly these two. The filter panel's `<details>`
  needs nothing of the kind — its summary is not dressed as a button, so its marker is wanted.
- `screenreader-only` is now load-bearing in one more place: it hides the sentence explaining why
  the last remaining column will not turn off. Without the rule, that sentence is on screen for
  everyone.
- The new Dataperiode column needs `variable-dataitem-main__period`, alongside the `__code`,
  `__dataType` and `__status` width modifiers already outstanding with helsedata.
  `variable-dataitem-header__period` they already have. Both sample hosts show a working
  approximation. (Fhi.Metadata-35oil)
