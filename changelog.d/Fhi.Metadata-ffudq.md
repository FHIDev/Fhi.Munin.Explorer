category: Changed
- **The kilde table no longer prints each kilde's code under its name; Kode is an optional column,
  off by default.** The code under every name was noise in the list (sak #6076). It is now the
  first column in the column picker and starts unticked; turned on, it is a plain cell straight
  after Navn. Searching on a code still finds the kilde whether the column is on or off. No new
  class name, and the default table's column count is unchanged. The scroll box's column-count
  modifier now runs one higher: with every column on it reads sixteen with the selection column and
  fifteen without. The stylesheet's width thresholds stop at fifteen with and fourteen without, so
  that widest table has no threshold rule. It keeps the base box's own horizontal scroll at every
  width instead of opening out and pinning its header on a very wide container.
  (Fhi.Metadata-ffudq)
