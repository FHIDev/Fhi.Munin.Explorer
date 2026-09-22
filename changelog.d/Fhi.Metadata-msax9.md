category: Changed
- **A dataperiode with an unknown start now reads "? – <slutt>" everywhere, and a missing date
  never reads as the year 1.** The result row, the panel's period bar, the saved-list cell and the
  variable page each composed the range themselves and disagreed: three wrote "?" for a missing
  start where the shared helper let the end stand alone, and three drew `default(DateTimeOffset)`
  as 1. jan. 0001 where the helper read it as no date. All four now call `CatalogueDate.Period`,
  which writes the "?" — an explicit question mark says the catalogue gave no start, where an end
  standing alone reads as a start and a bare dash reads as a value that failed to draw. The same
  reading now applies to every other field drawn through that helper: a kilde's and a datasamling's
  validity, a kilde's dataperiode, and a datasamling membership in the open panel. A period with
  neither end still says "Ikke oppgitt", and an open end still says "Pågående", whichever way the
  payload carries the absence. (Fhi.Metadata-msax9)
