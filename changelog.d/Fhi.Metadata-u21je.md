category: Notes for hosts
- **A legacy Blazor Server host on .NET 10 needs `PersistedCircuitInMemoryMaxRetained = 0` and a `#blazor-error-ui`.**
  Without them, a reader who comes back after the network was gone longer than the circuit retention gets a page that
  no longer answers and says nothing: .NET 10 "resumes" the expired circuit with none of the components. With the
  option the resume is refused and `blazor.server.js` reloads the page, the explorer's state coming back from the URL.
  `samples/LegacyHost` and `samples/HostileHost` now carry both, and the README has the two snippets under «A legacy
  Blazor Server host on .NET 10». Reported on helsedata test (FHIDev/Munin#6911). (Fhi.Metadata-u21je)
