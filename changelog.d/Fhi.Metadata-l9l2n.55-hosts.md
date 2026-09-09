category: Notes for hosts

- **The kilder table's rows are click targets now, and no host stylesheet says so.** No class name
  changed and no rule is required, but a row that opens on a press wants `cursor: pointer` — the
  package ships no CSS and cannot supply it, so on `Fhi.Helsedata.Stiler` the pointer stays an
  arrow until the rule lands there. Every behaviour the row press reaches is still on the chevron
  button beside it, which is in the tab order and unchanged, so the missing cursor costs
  discoverability and nothing else. (Fhi.Metadata-l9l2n.55)
