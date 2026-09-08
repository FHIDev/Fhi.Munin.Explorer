category: Fixed

- **Creating or renaming a variable list no longer risks swallowing an unrelated save or removal
  raised by another surface while it is in flight.** The two page reads a create used to skip, and
  the one a rename did, were suppressed by counting notifications rather than naming them, so a
  same-numbered but unrelated `VariableListState` change during that window could be silently
  dropped instead of redrawing the list on screen. Suppression is now by identity - which list a
  change names and whether it could touch that list's rows - carried as the argument of the
  `Changed` event. (Fhi.Metadata-wuxkn)

- **BREAKING for a host that subscribes to `VariableListState.Changed`: it is
  `Action<VariableListState.ListChange?>` now, not `Action`.** A handler written as `() => ...`
  no longer compiles; take the argument and ignore it (`_ => ...`) to keep the old behaviour. The
  identity travels on the event rather than in a property beside it because these methods await
  with `ConfigureAwait(false)`, so a shared property could belong to the next raise by the time a
  handler read it - and reading the wrong one skips a reload, which is the defect above.
  (Fhi.Metadata-wuxkn)
