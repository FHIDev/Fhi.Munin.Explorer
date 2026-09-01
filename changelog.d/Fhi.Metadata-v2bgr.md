category: Fixed

- **A facet you have chosen from stays on screen when the selection matches nothing.** The counts are
  cross-filtered against the whole selection, so a selection returning zero rows made the API report
  nothing for every facet — the chosen value included, name and all — and the panel dropped them.
  The reader was left filtering by something they could neither see nor undo, with the address bar
  as the only way out. Measured against skytest: one kilde plus a date matching nothing took the
  kilde facet from 43 entries to none. (Fhi.Metadata-v2bgr)
- **The counts disappear rather than going stale.** While a selection matches nothing, the controls
  on screen are the ones the reader was last offered; the numbers beside them would describe a
  different moment, so they are not shown. They come back as soon as the API has something to say.
- A reader who **arrives on a link that already matches nothing** has no previous answer to keep, so
  the panel asks once what the catalogue holds at all. Without it a shared link could strand
  whoever opened it, which is the harder half of this to notice.
