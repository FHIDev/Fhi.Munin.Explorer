category: Changed
- **Share codes of 10 characters open beside the 6-character ones.** `SharedList.NormalizeCode`, and so the
  `?delekode=` URL state, the field for opening a shared list and `GetSharedListAsync`, accept 6 or 10 ASCII letters
  or digits. Munin will mint 10-character codes once helsedata pins this version; codes already handed out keep
  working until they expire. `SharedList.CodeLength` is obsolete and removed in 3.0.0. (Fhi.Metadata-ayqlp.7)
