category: Fixed

- **A variable's statistics now show in the result row's Data tab**, beside the kodeverk, as Runa
  shows them. The tab drew kodeverk alone and left the numbers one click further in, inside the
  whole-variable view — so a reader who opened a row to see what its values look like was told
  nothing about them. A variable with no statistics draws no heading and no empty table, exactly
  as the full view already behaved. (Fhi.Metadata-isvb7)
- **The statistics heading and table are now one shared block** rather than a section only the
  whole-variable view knew how to draw. The emptiness check lives inside it, so the two surfaces
  cannot drift apart on the question of what an absent set looks like. No markup changed in the
  full view.
