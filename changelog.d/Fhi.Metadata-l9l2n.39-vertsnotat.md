category: Notes for hosts
- **Two renames, one line each.** A host mounting `VariableExplorerWithUrlState` mounts
  `VariableExplorer` instead — same parameters, plus the Variabelliste tab. A host that deliberately
  wanted the bare search component under the old `VariableExplorer` name now names `VariableSearch`.
  Nothing else moves: the CMS field whose default is `Fhi.Munin.Explorer.Blazor.VariableExplorer`
  becomes correct without being touched.
  <br><br>
  It must be mounted at an **interactive render mode** — `render-mode="Server"`, never
  `ServerPrerendered` — because it now owns the query string, and it throws on initialisation
  rather than drawing a page whose URL never follows the view. Pass `IsAuthenticated`, or the
  Variabelliste tab is empty by design.
  <br><br>
  **No new class names.** The tablist wears `munin-explorer-meta__tabs`, `munin-explorer-meta__tab`,
  `munin-explorer-meta__tab--active` and `munin-explorer-meta__tab-content`, which the detail
  panel's tabs already wear, so `Fhi.Helsedata.Stiler` needs no new rule for this change.
