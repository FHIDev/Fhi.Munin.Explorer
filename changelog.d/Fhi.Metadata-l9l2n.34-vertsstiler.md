category: Notes for hosts

- **`button-square--ghost-blue` is a new name to style**, and it is Stiler's own rather than one
  this package invented. A host on Stiler already has it; a host with a stylesheet of its own draws
  the drilldown's way back without the variant's colour until it adds a rule — the element still
  carries `hd-button-square`, so whatever base button rules the host has keep applying. Both sample
  stylesheets carry it. (Fhi.Metadata-l9l2n.34)
- **The ghost buttons that stay ghosts want a border, and both sample stylesheets now draw one:**
  `1px solid` at `--grey60` scoped to the component's root section, which is 6.76:1 on the page
  ground and clears WCAG 1.4.11's 3:1 for a border that identifies a control. The shared
  `.button-square--ghost` is deliberately untouched — it is helsedata's shape and is used far
  outside Munin.
- **This is not in Stiler yet.** Nothing in this repository can see `Fhi.Helsedata.Stiler`, so green
  CI here is not evidence the buttons are bordered on helsedata.no. The matching rule is open as PR
  39031 in that repo; until it merges, a host on Stiler still sees the old borderless ghost.
