category: Added
- **A datasamling in the kildeutforsker's hierarchy can be opened on its own page.** Each
  datasamling in `KildeView`'s tree now carries a link beside its name, and `KildeExplorer` reads
  and writes `?datasamling=` beside `?kilde=` so the address names the whole path the reader
  walked. It is a plain `<a href>`: middle-click and Ctrl+click open a tab, the address pastes into
  a fresh one, and the browser's Back button returns to the kilde rather than to the list. The
  `<summary>` is untouched and still expands and collapses on Enter and Space. `DatasamlingView`,
  which the variable explorer already reached, is rendered unchanged — the route was what was
  missing, not the view. `KildeSearch` gains `SelectedDatasamlingId` and `DatasamlingHref` for a
  host that owns its own query string. (Fhi.Metadata-l9l2n.107)
