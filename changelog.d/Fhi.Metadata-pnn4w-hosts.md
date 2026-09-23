category: Notes for hosts
- **`munin-explorer-kilde__kildetype` is no longer emitted by any component.** A host with a rule
  for it can drop that rule — it now matches nothing, on this package's own pages and inside the
  variabelutforsker's drill-in panel alike. The kildetype is drawn by the hero fact row instead,
  which wears `munin-explorer-page__facts` and is already styled; nothing new needs a rule, and both
  sample stylesheets have had the badge rule removed. `Fhi.Helsedata.Stiler` still carries its copy,
  which is harmless and is removed separately. (Fhi.Metadata-pnn4w)
