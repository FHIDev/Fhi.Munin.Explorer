category: Fixed
- Accessibility pass over `Variabelutforsker`. The result summary now names the search it
  describes and says when only the first page is shown; failures are announced assertively
  through a `role="alert"` region instead of politely alongside the count; the result table has
  an accessible name; its horizontally scrolling wrapper can be reached and scrolled from the
  keyboard; the Søk button is no longer disabled mid-search, which used to drop focus to
  `<body>`; empty cells read as "Ikke oppgitt" rather than as an em dash; and result rows are
  marked `lang="no"` so Norwegian variable names are not read by an English synthesiser.
