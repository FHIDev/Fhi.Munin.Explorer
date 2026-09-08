category: Fixed

- **Creating or renaming a variable list no longer risks swallowing an unrelated save or removal
  raised by another surface while it is in flight.** The two page reads a create used to skip, and
  the one a rename did, were suppressed by counting notifications rather than naming them, so a
  same-numbered but unrelated `VariableListState` change during that window could be silently
  dropped instead of redrawing the list on screen. Suppression is now by identity - which list a
  change names and whether it could touch that list's rows - via the new `VariableListState.LastChange`.
  (Fhi.Metadata-wuxkn)
