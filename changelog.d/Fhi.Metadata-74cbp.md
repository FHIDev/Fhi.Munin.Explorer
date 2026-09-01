category: Fixed

- **The open kilde reads as loading from the click that opens it**, not from the moment its fetch
  starts. `KildeExplorer` raised `SelectedKildeIdChanged` before starting the detail request, so a
  host that does anything asynchronous in that handler — writing the URL, as both sample hosts do
  — got one render of the drilldown with `aria-busy="false"` over an empty status line, announcing
  a finished and empty lookup that had not been made. (Fhi.Metadata-74cbp)
