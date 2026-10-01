category: Notes for hosts
- **`VariableListView` takes an optional `VariableHref`.** A `Func<VariableListItem, string>` returning an absolute address that opens one variable. `VariableExplorer` passes its own, so a host using it needs no change; one that declines the `variabelId` query key gets no share controls in the list panel.
