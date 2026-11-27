category: Fixed
- **A host that serves no `explorer-interop.js` is no longer asked for it on every render of a saved list.** Where the
  host answers the module import with a 404 or a Content-Security-Policy refusal, `VariableListView` no longer asks
  for it again on each render after the first refusal, both for the press watch that decides where focus goes after
  «Fjern» and for the watch that closes its menus on a press outside them. Before, each render of a saved list or the
  browser list repeated the import. A circuit that is reconnecting is still asked again on a later render. Nothing
  changes for a host that serves the module. (Fhi.Metadata-emsc0.3.27)
