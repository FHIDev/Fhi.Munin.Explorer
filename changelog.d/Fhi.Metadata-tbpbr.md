category: Fixed

- **Kelda's kategori and tilgangsnivå facets read the catalogue's own vocabulary** - the words on
  those checkboxes were transcribed into the package, which made them right on the day they were
  written and out of date from then on: a category the catalogue added afterwards showed as
  `ehds-cat:` in the facet while the kilde view one click away showed its Norwegian word, from the
  live vocabulary the API sends with a kilde. `KildeExplorer` now fetches that same vocabulary
  beside the list and both surfaces read it, so a value the catalogue adds is a word in the panel
  the day it is added. The transcribed table is gone. (Fhi.Metadata-tbpbr)
- **A token is matched whole rather than from the last colon on** - the transcribed table was keyed
  on the bare token, so `annet-vokabular:biobanks` read as "Biobanker" in the facet and as itself in
  the detail panel. One value, two labels, depending on which screen the reader was on. Two prefixes
  over one bare token are two values in the catalogue, and both surfaces now say so.
  (Fhi.Metadata-tbpbr)
- **A value the vocabulary does not list is unchanged: it keeps its checkbox, its count and its
  token**, unmarked by `lang` because a CURIE is prose in no language. So is what happens when the
  vocabulary cannot be fetched at all - the facets fall back to the catalogue's tokens and the list
  itself is unaffected, since the two are separate calls that fail apart. (Fhi.Metadata-tbpbr)
