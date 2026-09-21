category: Notes for hosts
- **The contents nav's mark is drawn by a Fhi.Helsedata.Stiler rule that no published package was
  checked to carry.** Stiler draws the current entry from `.munin-explorer-page__toc li a[aria-current]`,
  which reached Stiler's `main` on 2026-09-21. Until a host runs a Stiler release that includes it,
  the attribute is set and nothing looks different. The contents column also gains a per-instance
  id, `munin-explorer-contents-*`, which the module uses to find it. (Fhi.Metadata-35w0p.15)
