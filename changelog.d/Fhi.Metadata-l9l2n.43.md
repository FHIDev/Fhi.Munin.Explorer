category: Changed

- **BREAKING for a host that derives from a component: every published component is now `sealed`.**
  `VariableExplorer` and `KildeExplorerWithUrlState` were the last two that were not — the other six
  already were — and their openness was silence rather than a decision: neither file said why, and
  neither had ever carried the keyword. Nothing in this package, the samples or helsedata derives
  from either, and helsedata mounts by type name out of a CMS field rather than by inheritance, so
  the door was open for no consumer that exists. Unsealing later is invisible; sealing later is a
  break, which is why alpha is the moment to decide it. If you do need to derive from one, say so
  and it can be reopened with a reason attached. (Fhi.Metadata-l9l2n.43)
