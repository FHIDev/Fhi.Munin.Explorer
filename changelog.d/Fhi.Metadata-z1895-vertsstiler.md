category: Notes for hosts

- **A rule for `munin-explorer-filters__count` has to switch off shrinking and wrapping.** The
  facet value's `<label>` is a flex row carrying `overflow-wrap: anywhere`, so that a
  200-character databehandler breaks inside the sidebar rather than leaving it. The count sits in
  that row as a shrinkable item and inherits the wrapping, so a long value squeezed it until
  `(32)` broke between the digits and the closing bracket — two lines, reading as two numbers.
  `flex: none` and `overflow-wrap: normal` on the count are what hold it whole, and the sample
  stylesheets now carry both. Taking the wrapping off the label instead fixes the count and lets
  the long values out of the panel. (Fhi.Metadata-z1895)
