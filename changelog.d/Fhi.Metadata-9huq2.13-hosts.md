category: Notes for hosts
- **A host that mounts `KildeSearch` itself must wire `ExploreSelectionRequested` to keep marked datasamlinger.**
  Pass the filter on to the variable explorer as its query string, for example
  `ExploreSelectionRequested="@(filter => Navigation.NavigateTo($"{variablesPath}?{filter.ToQueryString()}"))"`,
  next to the `ExploreVariablesRequested` you already wire, and from an interactive parent: a callback created in a
  statically rendered parent arrives empty. Without it, and without the obsolete `ExploreDatasamlingerRequested`, no
  datasamling can be marked. Keep `ExploreDatasamlingerRequested` only until the new one is wired; setting both is
  allowed, and the new one wins. A host that mounts `KildeExplorer` with `VariableExplorerPath` set needs to do
  nothing. (Fhi.Metadata-9huq2.13)
