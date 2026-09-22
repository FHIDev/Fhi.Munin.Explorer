category: Changed
- **The kilde table no longer prints each kilde's code under its name; Kode is an optional column,
  off by default.** The code under every name was noise in the list (sak #6076). It is now the
  first column in the column picker and starts unticked; turned on, it is a plain cell straight
  after Navn. Searching on a code still finds the kilde whether the column is on or off. No new
  class name. The default table's column count is unchanged; with every column on, the scroll
  box's column-count modifier now reads sixteen rather than fifteen, a count no stylesheet has a
  rule for, so that box keeps scrolling as every wide configuration already did.
  (Fhi.Metadata-ffudq)
