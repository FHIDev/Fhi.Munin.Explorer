category: Notes for hosts
- The packages now ship their XML documentation, so the rules that matter show up in IntelliSense
  at the call site rather than only in this repository — including that an
  `IMuninExplorerTokenProvider` must be singleton-safe and must not reach for
  `IHttpContextAccessor`.
- Each package has a real description on nuget.org, and `Fhi.Munin.Explorer.Blazor` says plainly
  that it needs `Fhi.Munin.Explorer.Client` (or your own `IMuninExplorerClient`) to have anything
  to show.
