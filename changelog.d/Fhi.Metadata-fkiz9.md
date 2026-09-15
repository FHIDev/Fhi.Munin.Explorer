category: Fixed
- **The contents nav now lists the sections the explorers add to a detail page.** In the
  kildeutforsker the nav on an open kilde stopped at Statistikk, although Variabler — and with
  `ShowAccessAndPrices`, Kriterier for tilgang til data and Priser — were drawn below it, and the
  whole-variable page in the variabelutforsker left out its Kodeverk section the same way. Each is
  now a section of its own with a fixed English id (`variables`, `accesscriteria`, `prices`,
  `codelists`), listed in page order, and Kodeverk is listed only for a variable that has one.
  Runa's kilde page, which adds no sections, lists only the view's own. (Fhi.Metadata-fkiz9)
