category: Added
- **A datasamling page now lists the collection's variables, a page at a time, under the section
  that already holds "Antall variabler".** The payload states how many variables a collection has
  and names none of them, so the table is a second call the view makes for itself:
  `SearchVariablesAsync` narrowed to that datasamling, at the same page size the result list uses,
  with historical versions left out so the total agrees with the count drawn beside it. Four
  columns — Kode, Navn, Beskrivelse and the datatype in the reader's own language rather than the
  code the catalogue stores. It is drawn inside the section the catalogue declares for that count's
  neighbours — "Variabler" on today's data — so the page gains no heading of this package's, and a
  section the placement named and this collection filled nothing into is the table's under the
  curator's own name rather than an empty one dropped. A payload predating those rows draws the
  table beside the count in the view's own statistics block, and one that counts nothing and places
  nothing gets the view's own "Variabler" section, because a table fetched and drawn nowhere is the
  one outcome none of this is worth having. Opening a different collection resets it to the first
  page and cancels the call in flight, a load that fails says so and offers a retry rather than
  reading as a collection with nothing in it, and a collection that really has none says that
  instead. A collection that loses rows while the reader is deeper in it steps them back to a page
  that still has some, and the pager stays on screen through that step even where the smaller count
  would no longer draw one, so the button under the reader's finger is not taken out of the
  document. Where there is nowhere to step back to — page one itself, or a server that answers a
  second page with nothing too — the live region says this page has no variables although the
  collection has, rather than settling into a section with a heading, a pager and nothing between
  them.
  (Fhi.Metadata-mg08i)
