category: Notes for hosts
- **Each mounted `KildeView`, `VariableView` and `DatasamlingView` makes one `/api/explorer/filters` request per language.** It goes through the registered `IMuninExplorerClient` and nothing else is needed. A host that mounts `VariableView` or `DatasamlingView` without registering a client still renders them, with the kildetype word shipped in this package.
