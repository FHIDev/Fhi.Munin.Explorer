category: Fixed

- **Going back from an open kilde is no longer undone by the fetch that follows it.**
  `KildeExplorer` asked for the detail of the kilde the click carried rather than the one still
  open, so with a host that does anything asynchronous in `SelectedKildeIdChanged` — writing the
  URL, as both sample hosts do — a reader who pressed Back inside that window had a request issued
  for the kilde they had just left. (Fhi.Metadata-8wpau)
