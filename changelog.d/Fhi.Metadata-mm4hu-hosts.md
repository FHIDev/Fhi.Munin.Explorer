category: Notes for hosts

- **The saved-list filter panel has to be a direct child of the explorer's own section, which is why
  it is a fragment `VariableSearch` draws and not something `VariableListView` draws for itself.**
  `Fhi.Helsedata.Stiler` places the filter column with `.munin-explorer > .munin-explorer-filters`,
  a child combinator, and the saved list renders two levels down inside the tab panel. A host
  mounting `VariableListFilters` on a page of its own must put it where the search's own panel would
  go, or the panel lands in the result column and the 384px filter track stands empty — which is
  what that tab looked like before this: 408px of gutter on the left and none on the right.
- **No new class name.** The panel wears `munin-explorer-filters`, `form-element__label`,
  `munin-explorer-filters__count` and `hd-button-square button-square--ghost`, all of which Stiler
  and both sample stylesheets already draw. Nothing to add.
