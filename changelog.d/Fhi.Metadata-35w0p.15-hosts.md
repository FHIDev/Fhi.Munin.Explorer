category: Notes for hosts
- **The contents nav's mark needs Fhi.Helsedata.Stiler 0.1.89 or later.** Stiler draws the current
  entry from `.munin-explorer-page__toc li a[aria-current]`, first published in 0.1.89. On an older
  release the attribute is set and nothing looks different. The contents column also gains a per-instance
  id, `munin-explorer-contents-*`, which the module uses to find it. (Fhi.Metadata-35w0p.15)
