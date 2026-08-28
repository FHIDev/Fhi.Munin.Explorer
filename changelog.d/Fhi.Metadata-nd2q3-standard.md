category: Changed
- **`VariableExplorer.PageSize` now defaults to 20, not 25.** A host that never set the parameter
  will show 20 rows a page where it showed 25, and should set `PageSize="25"` if the old size
  matters to it. The default has to be one of the sizes the new control offers: left at 25 it would
  have drawn three buttons with none of them pressed on first load, which is truthful — no size the
  reader can choose is in force — and reads as broken. 20 is the middle of the three and Runa's own
  starting size, so the two explorers now open the same way for the same person.
- `IMuninExplorerClient.SearchVariablesAsync` still defaults `pageSize` to 25, which is Munin's own
  API default and unrelated to what the component asks for. Only the component's default moved.
  (Fhi.Metadata-nd2q3)
