category: Notes for hosts
- The column picker adds eight class names a host outside helsedata's estate has to provide. Seven
  are helsedata's own, from the `variables.css` their variable page carries —
  `variable-explorer-header` with `__actions` and `__actions-button`,
  `variable-explorer__dropdown`, and `dropdown-choicepicker` with `--right` and `__item` — and
  `form-control__label` is Stiler's. All of them were read back off the compiled stylesheets rather
  than off a list of names. `sortable-dropdown` is deliberately *not* among them, although it looks
  like the obvious fit: it is helsedata's mobile sort control, `display: none` above 1280px, so a
  picker wearing it would be invisible on every desktop.
- The open list is `position: absolute`, and the wrapper carries an inline `position: relative` so
  it anchors to the picker rather than to whatever the host page happens to have positioned above
  it. That is what helsedata's own markup does inline too. A host that styles none of these names
  still gets a working picker — it is a `<details>`, a `<ul>` and buttons in two states, the same
  three shapes the filter panel leans on — drawn in the flow instead of over the list.
- `screenreader-only` is now load-bearing in one more place: it hides the sentence explaining why
  the last remaining column will not turn off. Without the rule, that sentence is on screen for
  everyone.
- The new Dataperiode column needs `variable-dataitem-main__period`, alongside the `__code`,
  `__dataType` and `__status` width modifiers already outstanding with helsedata.
  `variable-dataitem-header__period` they already have. Both sample hosts show a working
  approximation. (Fhi.Metadata-35oil)
