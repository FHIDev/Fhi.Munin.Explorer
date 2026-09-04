category: Changed

- **`VariableSearch` is sealed.** It now unsubscribes from `VariableListState.Changed` in
  `Dispose`, and an unsealed disposable owes CA1063 a virtual pattern a Blazor component has no use
  for — the same reason `VariableListView` is sealed. Mounting it is unaffected; only a host that
  derived from it has to stop. (Fhi.Metadata-ehghv)
