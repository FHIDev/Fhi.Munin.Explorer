category: Notes for hosts
- **`VariableFilter` gains `OrTreeTicks`, written as `treeTicks=or` in the explorer's address.** It is in
  `VariableFilter.QueryKeys`, so a host that keeps its own query parameters apart from the explorer's needs no change.
  It is written only when two or more tree ticks are set, and it does not count in `ActiveCount`.
