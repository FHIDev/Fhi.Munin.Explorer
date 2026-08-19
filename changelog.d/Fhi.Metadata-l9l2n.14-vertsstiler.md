category: Notes for hosts
- **The detail panel introduces one handle and no style names, and needs base element styling
  instead** - it is a `<dl>` of labels and values, an `<ol>` for the kilde trail and a `<ul>` for
  the variabelgrupper and kodeverk, wearing Stiler's `form-element__label`, `caption`, `infobox`
  and the same ghost `hd-button-square` the sort and facet buttons use. Stiler has no definition
  list, no breadcrumb and no key/value block that can be read back off its compiled stylesheet, and
  the standing rule is that a class name goes into the markup only once it has been read off the
  host's CSS. So a host supplies base styling for those three elements — in particular the trail,
  which without a rule renders as a numbered list rather than as a path. `variable-explorer-detail`
  is the third handle of ours in the DOM, alongside `variable-explorer` and
  `variable-explorer-filters`, and carries no styling. Both sample hosts show a working
  approximation. (Fhi.Metadata-l9l2n.14)
- **A result card now contains a button** - the disclosure that opens the panel, one per row, and
  never `disabled` — including while its own fetch runs, for the same focus reason the pager's
  buttons carry `aria-disabled`. A host stylesheet that assumed a card held no interactive element
  should check its `:hover` and `:focus-within` rules; Stiler's `datasourcecard` already has both.
  (Fhi.Metadata-l9l2n.14)
