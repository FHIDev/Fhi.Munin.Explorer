category: Changed

- **One package instead of three.** `Fhi.Munin.Explorer` now carries the component, the client that
  feeds it and the types they share. Replace references to `Fhi.Munin.Explorer.Blazor` and
  `Fhi.Munin.Explorer.Client` with the single package; namespaces are unchanged, so no `using` has
  to move.
- **Supplying your own `IMuninExplorerClient` still works** — the interface is unchanged, and a host
  that registers its own implementation never touches the built-in one. What went away is the
  version matrix and the half-installed state where the component rendered with nothing behind it.
