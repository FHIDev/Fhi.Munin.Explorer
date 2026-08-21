category: Notes for hosts

- **The kilde view's nine invented class names now have example rules in both sample hosts.** They
  had none. The view arrived with `variable-explorer-kilde` and eight `__`-suffixed names of its
  own — a header block, identifiers, kildetype, description, a body split into `__main` and
  `__aside`, and the `__datasamlinger` table — and neither sample styled any of them, so both drew
  the view at raw browser defaults: the sidebar stacked under the main column, the kildetype tag
  reading as a paragraph. These are names Stiler has never heard of and helsedata's `variables.css`
  has no kilde section to borrow from, so a host outside their estate owes rules for all nine; the
  samples now show a working approximation of each. The layout is two columns above 1024px and one
  below, the same threshold the filter panel already uses.
- `variable-explorer-period` — the wrapper around the period bar, as distinct from its `__range`,
  `__track` and `__fill` — was in the same position and is styled now too.
- **The sample stylesheets ask for palette tokens bare**, as `var(--grey30)` rather than
  `var(--grey30, #e6e6ed)`. The declarations are in the same file, so a fallback could never fire
  and could only disagree — and four of the six did. One of them, `var(--grey70, #5a5f78)`, named a
  token nothing declares, so five rules painted a colour that is not in the Stiler palette the file
  claims to reproduce. Those ask for `--grey60` now. Nothing a host has to copy changed; what
  changed is that the file no longer misstates its own colours to whoever reads it as a reference.
- `scripts/assert-sample-css-in-step.sh` checks both halves of the sample-stylesheet invariant now:
  that the two copies are byte-identical, and that between them they style every
  `variable-explorer*` name the package invents. The second is what would have caught the kilde gap
  — two copies can agree perfectly about a block neither of them has. (Fhi.Metadata-ktixw)
