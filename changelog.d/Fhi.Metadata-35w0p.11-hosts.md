category: Notes for hosts
- **Move any rule you keyed on `munin-explorer-meta__grid` for a detail page to
  `munin-explorer-page__fields`.** The kilde, datasamling and variable views no longer put the
  panel's class on their fact lists, so a rule of yours reaching one through that name stops
  matching — quietly, because the element is still there and still a definition list. The same
  move applies to `munin-explorer-meta__language`, which becomes `munin-explorer-page__language`
  on those pages. Both old names stay exactly where they were on the result row's drill-in panel,
  which still emits them, so a rule scoped to `.munin-explorer-meta` needs no change at all — and
  the kodeverk and frequency tables, which key off that ancestor rather than off the `dl`, are
  untouched on every surface. `Fhi.Helsedata.Stiler` 0.1.75 has rules for both new names already, in
  the same `components/munin-explorer/_page.scss` the chassis landed in. The fact list's are the
  panel's declaration for declaration — `1fr 1fr` with a 40px row gap and a 24px column gap, `font:
  normal 1rem/160%`, `margin-bottom: 24px`, and below 1280px one column, a 16px gap,
  `margin-bottom: 16px` and full width — but the language marker's is `margin: 0` alone, without the
  uppercase, letter-spacing and grey the panel's marker carries, so **a host that keyed nothing on
  either name still sees its language names change size and colour** on the three detail pages.
  `Fhi.Metadata-4ozhj` is the Stiler bead that puts them back; a host that wants them sooner can
  declare them itself. Both are handles otherwise: define neither and you get a definition list at
  browser defaults, with each language still on its own line because the name is a `<p>`. Both
  sample stylesheets stand in at Stiler's numbers, the bare language marker included.
  (Fhi.Metadata-35w0p.11)
