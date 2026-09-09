category: Changed

- **Nothing a host renders changes.** The facet checkbox's forced `checked` update is now guarded
  by a browser-driven check as well as by unit tests, which cannot see the browser's own flip of a
  box; and the comment beside that call named two refusal paths where only one needs it, since a
  rolled-back fetch corrects the DOM on its own. (Fhi.Metadata-1s7z1)
