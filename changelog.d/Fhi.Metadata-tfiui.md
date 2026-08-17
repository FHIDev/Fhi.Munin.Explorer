category: Added
- **Sorting in `Variabelutforsker`** - results can now be ordered by data source, data collection
  or variable group, in either direction, on top of the API's own default order the list starts in.
  Choosing the active field again reverses it, choosing another starts it ascending, and any change
  goes back to the first page — the same rules Runa's sortable column headers follow. There are no
  column headers here, so the ordering is a control of its own above the list, and the chosen order
  is spoken through the status line the component already had rather than through `aria-sort`, which
  does not exist without a header to put it on. The default order's button reads "Standard" rather
  than "Navn": the API's `name` sort groups by data source first and only then follows the
  catalogue's own sequence, so a name label would describe an order the list is not in.
  `IMuninExplorerClient.SokVariablerAsync` takes the new `SortField` and `SortDirection` and sends
  the API's own `sort`/`sortDir`; the Explorer API already ordered on both, with the variable code
  as a secondary key, so nothing changed there. (Fhi.Metadata-tfiui)
