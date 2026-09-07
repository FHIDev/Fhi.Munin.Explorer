category: Changed

- **The saved-list view reads as part of helsedata rather than as default markup.** Its variables
  are a real `<table>` with one row of `<th scope="col">` instead of a header line over a stack of
  `<div>`s that repeated all seven field names in every row; creating and renaming a list are
  behind their own buttons instead of two standing forms; the list on screen says how many
  variables it holds and when it last changed; and the buttons wear helsedata's own
  `button-square--primary` and `button-square--ghost-blue` where every one of them used to be
  `button-square--ghost`. The export block is unchanged — it is per-list, and helsedata has no
  equivalent to copy.
