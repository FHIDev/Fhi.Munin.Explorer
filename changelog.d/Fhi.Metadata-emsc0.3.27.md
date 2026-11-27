category: Fixed
- **A host that serves no `explorer-interop.js` is no longer asked for it on every render of a saved list.** Where the
  host answers the module import with a 404 or a Content-Security-Policy refusal, `VariableListView` stops asking for
  it after three refusals, separately for the press watch that decides where focus goes after «Fjern» and for the
  watch that closes its menus on a press outside them. Before, each render of a saved list or the browser list
  repeated the import. A single failed import is not taken as the host's answer: a later render asks again, and so
  does a circuit that is reconnecting. A host that serves the module is asked as before. (Fhi.Metadata-emsc0.3.27)
