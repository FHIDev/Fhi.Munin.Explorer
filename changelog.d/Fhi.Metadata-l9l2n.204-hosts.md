category: Notes for hosts
- **`VariableExplorer` reads and writes a new query key, `datasamling`.** It is in `ExplorerUrlState.ScalarQueryKeys`,
  so a host whose page already means something by `?datasamling=` can decline it through `DeclinedKeys`; the open row's
  datasamling is then written as `datasamlingId`, as before. Declining `datasamlingId` alone no longer keeps the
  datasamling out of the explorer's links: decline both for that. The code reaches a link only from a Munin that sends
  each datasamling's `code` on the variable detail. (Fhi.Metadata-l9l2n.204)
