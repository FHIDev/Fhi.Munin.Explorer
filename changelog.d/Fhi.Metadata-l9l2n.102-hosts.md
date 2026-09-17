category: Notes for hosts
- **BREAKING for hosts without Fhi.Helsedata.Stiler: the variable filter panel starts folded at
  every width unless the host carries the `munin-explorer-filters__*` fold rules.** The panel is folded behind
  "Vis filtre" by the `hidden` attribute, so a stylesheet with none of the rules shows the toggle
  and a folded panel on a desktop as well, while one with a reset such as
  `fieldset { display: block }` holds the panel open behind a toggle that says it is folded. `.munin-explorer-filters__facets[hidden]` needs
  `display: none`, and once there is room for a sidebar `.munin-explorer-filters__toggle` needs
  `display: none` while `.munin-explorer-filters__facets[hidden]` needs `display: block`.
  `Fhi.Helsedata.Stiler` already carries all three. The fieldset itself wears
  `munin-explorer-filters__facets`. The toggle sits beside that fieldset rather than inside it, so
  a host rule scoped under `.munin-explorer-filters` does not reach the button.
  (Fhi.Metadata-l9l2n.102)
