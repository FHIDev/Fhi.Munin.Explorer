category: Changed

- **BREAKING for a host that derives from a component: every published component is now `sealed`.**
  Counting from the released `0.1.0-alpha.8`, three change in this release — `VariableSearch` (see
  the bullet above), `VariableExplorer` and `KildeExplorerWithUrlState` — and five were already
  sealed. The last two were open by silence rather than by decision: neither file said why, and
  neither had ever carried the keyword. Nothing in this package or its samples derives from either,
  and helsedata mounts by type name out of a CMS field rather than by inheritance, so the door was
  open for no consumer we know of. Unsealing later is invisible to a consumer; sealing later is a
  binary break, which is why alpha is the moment to decide it. If you do need to derive from one,
  say so and it can be reopened with a reason attached. (Fhi.Metadata-l9l2n.43)
