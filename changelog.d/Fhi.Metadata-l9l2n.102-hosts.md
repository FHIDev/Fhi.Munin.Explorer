category: Notes for hosts
- **BREAKING for hosts without Fhi.Helsedata.Stiler: the variable filter panel starts folded at
  every width unless the host carries the kildeutforsker's fold rules.** The panel is folded behind
  "Vis filtre" by the `hidden` attribute, so a stylesheet with none of the rules shows the toggle
  and a folded panel on a desktop as well. `.munin-explorer-filters__facets[hidden]` needs
  `display: none`, and once there is room for a sidebar `.munin-explorer-filters__toggle` needs
  `display: none` while `.munin-explorer-filters__facets[hidden]` needs `display: block`.
  `Fhi.Helsedata.Stiler` already carries all three. The toggle sits beside the
  `munin-explorer-filters` fieldset rather than inside it, and the fieldset itself wears
  `munin-explorer-filters__facets`, so a host rule scoped under `.munin-explorer-filters` does not
  reach the button. (Fhi.Metadata-l9l2n.102)
