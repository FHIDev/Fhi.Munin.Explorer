category: Added
- **`Variabelutforsker` can now page through the whole result** - Forrige / Neste buttons below
  the results, with "Side 2 av 13" between them, so the 18 000 variables behind the first 25 are
  reachable. Changing the search or the ordering starts again at page one, and turning a page
  keeps both. There is no infinite scrolling and no page-size picker: the host still sets
  `SideStorrelse`, and the doc comment on that parameter says why the reader is not offered a
  choice. A `skiplink-pagination` anchor above the results jumps a keyboard user straight to the
  controls instead of making them tab through every card. (Fhi.Metadata-l9l2n.12)
