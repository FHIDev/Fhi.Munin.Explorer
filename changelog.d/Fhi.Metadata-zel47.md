category: Fixed
- **Double-clicking or shift-clicking "Vis filtre", "Lag ny liste", "Gi listen nytt navn" or
  "Slett listen" leaves each where the first click left it** - the kildeutforsker's filter panel
  and the saved-list view's three controls took no click count at all, so the second click of a
  double-click folded the panel, or shut the form, the first click had just opened. On the delete
  control it also worked the other way: the same button cancels the confirmation, so a stray
  second click re-armed the delete the reader had just called off. A shift-click extending a
  selection across the words beside them did the same. Both gestures are now refused the way the
  rows and the variable panel's disclosures already refused them; deliberate repeated pressing
  still toggles both ways, and Enter or Space on any of the four is unaffected.
  (Fhi.Metadata-zel47)
