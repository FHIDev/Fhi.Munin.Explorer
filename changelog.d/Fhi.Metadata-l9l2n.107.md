category: Added
- **A datasamling in the kildeutforsker's hierarchy can be opened on its own page.** Each
  datasamling in `KildeView`'s tree now carries a link of its own, and `KildeExplorer` reads and
  writes `?datasamling=` beside `?kilde=` so the address names the whole path the reader walked.
  It is a plain `<a href>`: middle-click and Ctrl+click open a tab, the address pastes into a fresh
  one, and Back returns to the kilde rather than to the list. The link is written in the node's
  `<li>` after the `<details>` and never inside the `<summary>`, so the disclosure keeps its one
  job and still expands and collapses on Enter and Space. Back lands on the kilde with its
  hierarchy collapsed again, not expanded to where the reader was. `DatasamlingView`, which the
  variable explorer already reached, is rendered unchanged — the route was what was missing, not
  the view. `KildeSearch` gains `SelectedDatasamlingId` and `DatasamlingHref` for a host that owns
  its own query string. (Fhi.Metadata-l9l2n.107)
