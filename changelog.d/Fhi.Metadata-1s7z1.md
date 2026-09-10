category: Changed

- **Nothing a host renders changes.** The forced `checked` update on the column picker's and the
  facet panel's checkboxes is now guarded by a browser-driven check as well as by unit tests, which
  cannot see the browser's own flip of a box; and the comment beside the facet call named two
  refusal paths where only one needs it, since a rolled-back fetch corrects the DOM on its own.
  (Fhi.Metadata-1s7z1)
