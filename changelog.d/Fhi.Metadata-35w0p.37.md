category: Added
- **Every column of the variable table's header now sorts.** Kode, Datatype, Status and
  Dataperiode join Navn, Kilde, Datasamling and Variabelgruppe, so `SortField` gains `Code`,
  `DataType`, `Status` and `DataPeriod`. **Their four wire tokens need a Munin API carrying
  Fhi.Metadata-0ayti**; an API older than that does not recognise them and falls back to its own
  default order silently, leaving the header announcing an ordering the list is not in. Half a
  table of headers
  responding to a press with nothing to tell the two halves apart is worse than none of them
  responding, which is why the four were never an optional half of this.
  Dataperiode orders by the START of the period the data covers — not by the range as it is
  written, where "1999" would file after "2021 – Pågående", and not by the version's validity
  window, which is a different fact about the variable. A variable with no datatype or no period
  start comes last whichever direction is asked for. Datatype and Status order by the catalogue's
  own code and the API's own status rule rather than by the words on screen, so neither moves when
  the reader switches language. Ordering stays the API's throughout: nothing in this package
  compares two rows, so a Norwegian name sorts in the catalogue's collation rather than in
  whatever culture the host's thread carries. (Fhi.Metadata-35w0p.37)
