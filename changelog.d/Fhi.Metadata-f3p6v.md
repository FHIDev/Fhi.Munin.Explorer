category: Added
- **`ExplorerUrlState` — the explorer state a URL carries, in one value.** Sits beside
  `VariableFilter` in `Contracts/` with the same `Parse`/`ToQueryString` pair, composing the filter
  with search, sort, direction, page and page size. It owns the default page size, so a host no
  longer keeps its own copy of that number to know what to leave out of a link.
