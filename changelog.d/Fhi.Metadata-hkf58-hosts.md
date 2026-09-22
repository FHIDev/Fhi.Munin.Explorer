category: Notes for hosts
- **`VariableExplorer` now owns `?instrumentId=` on the page it is mounted on, and no new class
  name comes with it.** The key opens one instrument's page, and it is declinable like every other
  scalar in `ExplorerUrlState.ScalarQueryKeys` — a host that already means something else by it
  passes it in `DeclinedKeys`, or mounts `VariableSearch` and owns the query string itself.
  Declining it draws a variable's instruments as plain words rather than as links, because the
  whole of an instrument's address is that one key and there is nowhere else for it to go. The
  instrument page is built on the `munin-explorer-page` chassis alone, so every rule it needs is
  one `Fhi.Helsedata.Stiler` already carries. A host composing `VariableSearch` itself supplies the
  two addresses, because only it knows where the explorer is mounted: without `InstrumentHref` a
  variable's instruments render as plain words rather than as links that go nowhere, and without
  `InstrumentVariablesHref` the instrument page draws no Variables section. Both must be supplied
  inside the interactive boundary, not from a static SSR parent. (Fhi.Metadata-hkf58)
