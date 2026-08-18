category: Notes for hosts
- **The filter panel introduces no new class names, and needs base element styling instead** - it
  is built from `<details>`, `<summary>` and nested `<ul>`s, with Stiler's `form-fieldset`,
  `form-element__label`, `caption` and the same `hd-button-square` / `button-square--secondary` /
  `button-square--ghost` pair the sort control already uses. That is deliberate: helsedata's own
  variable page styles its sidebar from `filter-search-explorer` in the page-specific
  `variables.css`, which is not a stylesheet this repository can read back, and the standing rule is
  that a class name goes into the markup only once it has been read off the host's compiled CSS.
  What a host has to supply is therefore base styling for those three elements — in particular list
  indentation, which is what shows a delkilde sitting under its kilde. Without it the panel still
  works and the hierarchy is still announced correctly; it just reads flat. Both sample hosts show a
  working approximation. (Fhi.Metadata-l9l2n.13)
- **A second name of ours appears in the DOM: `variable-explorer-filters`** - a handle, like the
  `variable-explorer` root, carrying no styling from this package or from Stiler. It is there so a
  host that can verify the sidebar names can place the panel without selecting on element position.
  (Fhi.Metadata-l9l2n.13)
- **Facet values are buttons with `aria-pressed`, not checkboxes** - so a host stylesheet has to
  draw the chosen state from `aria-pressed="true"` or from `button-square--secondary`, and the
  inert "Fjern alle filtre" button from `aria-disabled="true"` rather than from `:disabled`.
  (Fhi.Metadata-l9l2n.13)
