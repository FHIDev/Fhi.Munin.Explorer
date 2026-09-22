category: Changed
- **Runa's variable name now opens the whole variable, and a separate chevron opens the row panel.**
  This changes learned behaviour on a shipped surface: a reader who presses a variable's name used
  to get the inline panel under the row, and now gets the whole-variable view in place of the list,
  with "Tilbake til variabler" putting the list back with the same row open or shut as before. The
  panel moves to a chevron button, in a cell of its own first in each row, which carries `aria-expanded` and
  `aria-controls`; the name carries neither. Pressing the row strip still opens and closes the
  panel, as Kelda's row does. It reverses Fhi.Metadata-zqe14, which had put the chevron inside the
  name button, so that the two explorers split their gestures the same way. (Fhi.Metadata-35w0p.34)
