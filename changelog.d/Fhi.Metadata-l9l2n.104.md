category: Changed
- **The variabelutforsker's kildetype group counts wear `munin-explorer-filters__groupcount`
  instead of `munin-explorer-filters__chosen`.** The number beside a kildetype group in the Kilde
  facet is that group's size, drawn whether or not anything in it is ticked, while `__chosen` means
  how many values the reader chose — which is what the kildeutforsker's facet summaries use it for,
  and they keep it. One class had come to carry both meanings, so the markup told anyone reading it
  something false about half its uses. Both sample stylesheets add the new name to `__chosen`'s own
  selector list rather than giving it a block of its own, so the two cannot drift apart, and the
  merged `Fhi.Helsedata.Stiler` rule is written the same way — but it is in no published Stiler yet,
  so a host draws these counts at browser defaults until that release is cut. The Notes-for-hosts
  entry has the detail. (Fhi.Metadata-l9l2n.104)
