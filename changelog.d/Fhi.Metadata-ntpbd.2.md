category: Added
- **A saved variable list can be copied under a new name, and emptied.** `VariableListView` gains
  a "Kopier liste" disclosure beside rename, prefilled "{name} - kopi", which refuses an empty name,
  one over 200 characters, or one the reader already uses (trimmed, case-insensitive) before any
  write. The copy carries every variable, read a page of 1000 at a time, and not the "Ønskede data"
  annotations, which the disclosure says; afterwards the copy is shown and becomes the list the
  save buttons write to — not before, so they never write to a copy that is not on screen. If
  adding the variables fails part-way, the copy is still shown with what landed and the alert says
  it is incomplete — nothing is rolled back. A reader who chooses another list while a copy runs
  stays on it, even if they come back to the source, and an incomplete copy is then named in the
  alert rather than pointed at. A list deleted in another tab is neither copied nor reported as
  emptied. "Tøm liste" is confirmed in two steps the way "Slett listen" is, removes every variable
  while keeping the list and its name, and the search rows' save buttons redraw as unsaved. On an
  empty list both are `aria-disabled` with the visible reason "Listen er tom". No new class name.
  (Fhi.Metadata-ntpbd.2)
