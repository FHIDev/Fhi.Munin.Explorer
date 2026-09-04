category: Fixed

- **Removing a variable in `VariableListView` now reads as removed in the search results too.** The
  save button beside the same variable went on saying "Fjern fra liste" with `aria-pressed="true"`,
  offering to take out something already gone — reachable on any page that mounts both surfaces.
  The membership set is now maintained by `AddVariablesAsync` and `RemoveVariablesAsync` rather than
  only by the save press, and `VariableSearch` redraws on `VariableListState.Changed`. No host
  change needed, and no extra request: neither surface refetches. (Fhi.Metadata-ehghv)
