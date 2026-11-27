category: Fixed
- **A signed-in reader who moves on during a «Fjern» keeps their place.** Removing a row from a saved list used to
  pull focus back to the next row, the row before it or the list menu when the removal answered, even after the reader
  had pressed Tab or clicked elsewhere meanwhile. Focus now moves there only when it was lost with the removed row, as
  in the signed-out browser list.
