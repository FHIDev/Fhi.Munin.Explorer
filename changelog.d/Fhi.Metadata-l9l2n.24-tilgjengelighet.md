category: Fixed
- Accessibility pass over `Variabelutforsker`. The result summary now names the search it
  describes and says when only the first page is shown; failures are announced assertively
  through a `role="alert"` region instead of politely alongside the count; the result list has
  an accessible name; the Søk button is no longer disabled mid-search, which used to drop focus
  to `<body>`; a missing value reads as "Ikke oppgitt" rather than as an em dash; and Munin's
  own metadata is marked `lang="no"` so Norwegian variable names are not read by an English
  synthesiser.
