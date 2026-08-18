category: Added
- **`VariableExplorer` can now page through the whole result** - Forrige / Neste buttons below
  the results, with "Side 2 av 13" between them, so the 18 000 variables behind the first 25 are
  reachable. Changing the search or the ordering starts again at page one, and turning a page
  keeps both. There is no infinite scrolling and no page-size picker: the host still sets
  `PageSize`, and the doc comment on that parameter says why the reader is not offered a
  choice. A `skiplink-pagination` anchor above the results jumps a keyboard user straight to the
  controls instead of making them tab through every card. The pager stays on screen when a page
  turn fails, so the button that was just pressed is never removed under the reader's finger, and
  a page that comes back empty — an index that shrank between two requests, or an API that answers
  an out-of-range page with 404 — steps back to a page that has rows instead of reporting that
  nothing matched, keeping the pager even when that step back lands on a result with a single
  page. The position, both buttons and the row range all count from the page the server actually
  answered, so an API that clamps page 12 to page 8 cannot leave the caption describing different
  rows than the ones on screen; and if the step back fails in turn, the reader is put back on the
  page they turned from rather than left on the empty one. (Fhi.Metadata-l9l2n.12)
