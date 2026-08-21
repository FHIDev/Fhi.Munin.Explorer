category: Notes for hosts
- The accounting of the `variable-explorer*` prefix is now complete and split by what a host loses
  by ignoring a name. Six of them are helsedata's, from the `variables.css` their variable page
  carries: `variable-explorer-container`, `variable-explorer-results`, `variable-explorer-header`
  with `__actions` and `__actions-button`, and `variable-explorer__dropdown`. Everything else in
  the prefix is this package's, and no helsedata stylesheet has a rule for any of it. Most are
  handles the element does not need — the root `variable-explorer`, `variable-explorer-filters`,
  `-detail`, `-drilldown`, `-kodeverk*`, `-codes*`, `-group` and the nine `variable-explorer-kilde*`
  names in `KildeView` — because a Stiler class or a browser default already dresses it. The group
  headings, for one, are sized by the `headline headline-xxs` they also wear, so leaving
  `variable-explorer-group` undefined costs the eyebrow's look and nothing more. Two are not
  handles: `variable-explorer-crumb` is the link affordance on the kilde step of the trail (which is
  a `<button>`), and `variable-explorer-period__track` / `__fill` / `__track--ongoing` are the
  period bar itself — only its width comes from an inline style, so an undrawn bar renders as
  nothing at all. Earlier notes listed six invented names and said a host that defined none of them
  lost nothing visual; both halves were wrong. (Fhi.Metadata-e4bj2)
- `variable-explorer-source` is an element id prefix, not a class. The drill-in it names wears
  `variable-explorer-drilldown`, so a host or a test reaching for `.variable-explorer-source` finds
  nothing. Both sample hosts had rules written against it that had been dead since the kilde panel
  became a drill-in; they now select the drill-in.
- The package emits two `<table>`s, not one: the kodeverk code list in an opened panel, and the
  datasamlinger of a kilde in `KildeView`. The results list is neither — it is helsedata's
  `variable-data-list`, a `<ul>` with a header row of `<div>`s.
- The XML doc comments that ship with the package, which are what IntelliSense shows a consuming
  developer, still described the `datasourcecard` result shape that `Fhi.Metadata-zs56s` replaced.
  They now describe the DOM the components actually emit.
- `KildeView`'s four block headings wore `headline-sm`, a name neither Stiler nor helsedata's
  `variables.css` defines and the only class name in the package that appeared nowhere else — so
  the headings fell back to the browser's own `<h*>` size inside an otherwise styled page. They now
  wear `headline-s`, the same size the view's own name wears, because Stiler's scale has nothing
  verified between it and the `headline-xxs` the field labels wear.
- Both sample hosts now carry the Data tab's kodeverk rules. `ModernHost`'s `host.css` was missing
  the whole block, so opening a variable's Data tab there showed an unstyled kodeverk list and an
  uncapped code table — Kommunenummer's 885 rows pushed the rest of the page out of reach — and it
  read as a difference in the hosting model rather than the missing 76 lines it was.
