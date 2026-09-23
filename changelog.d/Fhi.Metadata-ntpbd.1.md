category: Added
- **A saved variable list can be shared as a six-character code and opened from a code or a
  `?delekode=` link.** `VariableListView` gains a "Del liste" disclosure beside rename and delete:
  it posts every variable of the list on screen to `POST api/explorer/lists/share` and shows the
  code, a link when the view has an address to put it in, a mailto link and the 90-day validity.
  The private "Ønskede data" annotation is left out of what is posted, since anyone holding the
  code can read it. An "Åpne delt liste" field opens a code, and a shared list is shown read-only
  under its own name with the eyebrow "Delt liste": the same table and pager, no remove buttons,
  no desired-data fields, and two actions — "Lagre som min liste", which refuses a name the reader
  already uses (trimmed, case-insensitive) before any write, and "Lukk delt liste". Signed out, a
  shared list is shown read-only with a sentence asking the reader to sign in to save it, and no
  `my/lists` call is made. The snapshot format is Runa's, so a code made in either frontend opens
  in the other. On an empty list "Del liste" is `aria-disabled` with a visible reason.
  (Fhi.Metadata-ntpbd.1)
- **`IMuninExplorerClient` gains `ShareListAsync` and `GetSharedListAsync`, and
  `ExplorerUrlState` gains `ShareCode`.** `GetSharedListAsync` answers null for an unknown,
  expired or malformed code and sends no request for a code that is not six ASCII letters or
  digits; it returns a new `SharedList` record. Both members carry default bodies that throw
  `NotSupportedException`, so a host implementing the interface itself still builds.
  `VariableListView` gains `ShareCode`/`ShareCodeChanged` (bindable) and `SharedListHref`;
  `VariableSearch` gains `ShareCode`, which draws the Variabelliste tab while a code is present,
  signed out too. (Fhi.Metadata-ntpbd.1)
