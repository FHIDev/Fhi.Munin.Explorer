category: Notes for hosts
- **Move any rule you keyed on `munin-explorer-meta__grid` for a detail page to
  `munin-explorer-page__fields`.** The kilde, datasamling and variable views no longer put the
  panel's class on their fact lists, so a rule of yours reaching one through that name stops
  matching — quietly, because the element is still there and still a definition list. The same
  move applies to `munin-explorer-meta__language`, which becomes `munin-explorer-page__language`
  on those pages. Both old names stay exactly where they were on the result row's drill-in panel,
  which still emits them, so a rule scoped to `.munin-explorer-meta` needs no change at all — and
  the kodeverk and frequency tables, which key off that ancestor rather than off the `dl`, are
  untouched on every surface. `Fhi.Helsedata.Stiler` has rules for both new names already, in the
  same `components/munin-explorer/_page.scss` the chassis landed in, copied from the panel's
  declaration for declaration: `1fr 1fr` with a 40px row gap and a 24px column gap, `font: normal
  1rem/160%`, `margin-bottom: 24px`, and below 1280px one column, a 16px gap, `margin-bottom: 16px`
  and full width. Both are handles: define neither and you get a definition list at browser
  defaults, with each language still on its own line because the name is a `<p>`. Both sample
  stylesheets stand in for Stiler's rules at those same numbers. (Fhi.Metadata-35w0p.11)
