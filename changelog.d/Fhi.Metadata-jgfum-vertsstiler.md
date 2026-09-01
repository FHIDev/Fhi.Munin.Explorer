category: Notes for hosts

- **Eight class names to style if you are not on Stiler.** `munin-explorer-datasamling` wraps the
  view; `munin-explorer-datasamling__header`, `munin-explorer-datasamling__identifiers` and
  `munin-explorer-datasamling__description` are the name block; `munin-explorer-datasamling__body`,
  `munin-explorer-datasamling__main` and `munin-explorer-datasamling__aside` are the main column and
  the sidebar; `munin-explorer-datasamling__criteria` is the inclusion-criteria paragraph. Seven of
  them want exactly what the kilde view's own seven want, so both sample hosts style the two
  together in one rule per line rather than twice. (Fhi.Metadata-jgfum)
- **All eight are handles rather than names carrying meaning nothing else carries.** A host that
  supplies no rule gets the sidebar stacked under the main column and prose at full window width —
  look, not information, and nothing that misreports a state.
- **These are not in Stiler yet.** Nothing in this repository can see `Fhi.Helsedata.Stiler`, so
  green CI here is not evidence the view is styled on helsedata.no. The rules have to land in
  Stiler under `components/munin-explorer/` the way the rest of the prefix did.
