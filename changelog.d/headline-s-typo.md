category: Fixed

- **Eleven class names that no stylesheet defines.** Nine block headings wore `headline-sm`, a typo
  for `headline-s`; the kildetype badge wore `tag`, and a tab wrapper wore `variable-meta__body`.
  None of the three is defined by helsedata's stylesheets or by Stiler, so each rendered unstyled
  inside helsedata.
- **A check that catches the next one.** The package's CSS checks only verified the names it
  invents; borrowed names had nothing watching them. `HostClassNames` renders each view and asserts
  every class in the DOM is one some stylesheet actually defines, against a capture of the 2,400
  class names helsedata's own bundles carry.
