category: Added

- **The reader chooses which columns the result list shows** - a Kolonner picker above the list,
  offering Runa's seven optional columns: Kode, Kilde, Datasamling, Variabelgruppe, Datatype, Status
  and Dataperiode. Navn is always there, because it is also the button that opens a row, and the
  last remaining column refuses to be hidden rather than leaving a list of nothing but names. The
  choice lasts as long as the page and is deliberately neither stored nor put in the host's URL,
  which is what Runa does today. (Fhi.Metadata-35oil)
- **Dataperiode is a column as well as a panel field** - the same two dates the open panel draws
  above its bar, so the column set is Runa's full seven. It is text rather than helsedata's bar,
  which is drawn entirely by rules this package does not ship. (Fhi.Metadata-35oil)
- **Status can now be shown even with historical variables filtered out** - the filter still decides
  where the column starts, and from the first press the reader's choice is what counts.
  (Fhi.Metadata-35oil)
