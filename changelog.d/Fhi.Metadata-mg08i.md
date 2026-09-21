category: Added
- **A datasamling page now lists the collection's variables, a page at a time, under the section
  that already holds "Antall variabler".** The payload states how many variables a collection has
  and names none of them, so the table is a second call the view makes for itself:
  `SearchVariablesAsync` narrowed to that datasamling, at the same page size the result list uses,
  with historical versions left out so the total agrees with the count drawn beside it. Four
  columns — Kode, Navn, Beskrivelse and the datatype in the reader's own language rather than the
  code the catalogue stores. It is drawn inside whichever section the catalogue's placement rows put
  the count in, which on today's data is "Variabler", so the page gains no heading, no section id
  and no contents-nav entry; a payload predating those rows draws it beside the count in the view's
  own statistics block. Opening a different collection resets it to the first page and cancels the
  call in flight, a load that fails says so and offers a retry rather than reading as a collection
  with nothing in it, and a collection that really has none says that instead.
  (Fhi.Metadata-mg08i)
