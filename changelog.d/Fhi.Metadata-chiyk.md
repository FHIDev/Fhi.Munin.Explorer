category: Notes for hosts
- **Set `ApiBaseUrl` to the explorer host: `https://explorer.munin.skytest.fhi.no` in test,
  `https://explorer.munin.sky.fhi.no` in production.** It serves only `/api/explorer/*`, which is
  everything the package calls. `runa` and `kelda` still answer the same API today, but they are
  the two UIs' hostnames and may stop being reachable from outside FHI, so a host that points at
  them should move before that. The README, `docs/running-locally.md`, the samples' development
  fallback and the startup error for a missing `ApiBaseUrl` now name the explorer host.
  (Fhi.Metadata-chiyk)
