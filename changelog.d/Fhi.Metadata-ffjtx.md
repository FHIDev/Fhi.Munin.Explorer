category: Fixed

- **The list showed the raw datatype code where the explorer showed its name** - a variable saved as
  datatype `2` rendered as `2` in `VariableListView`, next to a `VariableExplorer` calling the same
  variable `Heltall` on the same page. The list now reads the names from the API the same way the
  explorer does, and still falls back to the code when the API has no name for it.
  (Fhi.Metadata-ffjtx)
