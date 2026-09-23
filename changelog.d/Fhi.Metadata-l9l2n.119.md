category: Fixed
- **The list picker in the saved-list view no longer wears the facet panel's fold handle.** Its
  `<label>` carried `munin-explorer-filters__facets`, which is the name the two facet panels fold
  behind — the one a host keys its `[hidden]` and `> [role="group"]` rules on. Nothing about this
  label folds and it is never `hidden`, so neither of the two rules the sample stylesheets key on
  that name reaches it; whether a rule of yours does is worth a look, since a host's own
  stylesheet is the one thing this repository cannot read. The cost was that any change to how a
  host draws the handle would move or hide a control in an unrelated view, and that an audit of
  where the handle is used found a use that is not a fold. The label now carries no class of its
  own and still wraps the `<select>` it names; what draws it is the action row, whose
  `.munin-explorer-page__actions > label` rule is untouched and was doing the work all along. No
  `munin-explorer*` name is added, renamed or removed: `munin-explorer-filters__facets` is still
  the fold handle, now with two users instead of three. (Fhi.Metadata-l9l2n.119)
