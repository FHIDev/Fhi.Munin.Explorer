category: Fixed
- **The Kilde column names the kilde again where it has no kortnavn.** The Explorer API sends a
  missing kortnavn as an empty string rather than as null, so the fallback to the full kilde name
  never fired and the result list — and the saved-list view beside it — wrote "Ikke oppgitt" over
  a name they were already holding. Nearly three in five variabler are affected. The saved list's
  kilde filter fell the same way, leaving a checkbox whose whole accessible name was its count; it
  now says "Ikke oppgitt" where the list names a kilde neither long nor short, takes a later
  entry's name for a kilde whose first one carried none, and sorts by what the checkbox says.
  (Fhi.Metadata-l9l2n.62)
