category: Changed

- **The saved-list view reads as part of helsedata rather than as default markup.** Its variables
  are a real `<table>` with one row of `<th scope="col">` instead of a header line over a stack of
  `<div>`s that repeated all seven field names in every row; creating and renaming a list are
  behind their own buttons instead of two standing forms; the list on screen says how many
  variables it holds and when it last changed; and the buttons wear helsedata's own
  `button-square--primary` and `button-square--ghost-blue` where every one of them used to be
  `button-square--ghost`. The export block is unchanged — it is per-list, and helsedata has no
  equivalent to copy.
- **A rename or a removal now moves the list's "sist endret" as well as its name.**
  `VariableListState` patches its own copy of a list rather than refetching it, which is what makes
  a rename look instant — so the timestamp is patched with it. Without that the new line would keep
  naming the day the page was loaded while the name above it changed under the reader.
