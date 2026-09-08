category: Fixed

- **Datatype now reads in the page's own language instead of English.** `Streng`, `Heltall`,
  `Boolsk` and the rest were only matched against the numeric code a fully re-normalized variable
  carries; a variable still holding its pre-normalization value (`"String"`, `"Integer"`, …) fell
  through to the raw English word on a Norwegian page. Both codes and the legacy aliases now
  resolve to the same name, on the variable detail, list and datatype facet alike.
  (Fhi.Metadata-88fui)
