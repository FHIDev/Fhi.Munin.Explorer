category: Notes for hosts
- **`VariableExplorer` now owns `?delekode=` on the page it is mounted on.** The key is in
  `ExplorerUrlState.QueryKeys` and `ScalarQueryKeys`, so a host that already means something by it
  can pass `DeclinedKeys: ["delekode"]`; the explorer then neither reads nor writes it and offers
  no link beside a share code. Whether a host's sign-in returns the reader to the same address,
  and so keeps the code, is the host's. No new class name: the share and shared-list controls
  wear names already styled. (Fhi.Metadata-ntpbd.1)
