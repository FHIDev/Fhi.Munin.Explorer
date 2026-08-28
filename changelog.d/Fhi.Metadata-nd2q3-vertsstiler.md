category: Notes for hosts
- The size control adds one class name a host has to provide, `munin-explorer-pagination-size`, and
  it is the group's layout only — a host without the rule still gets a working control drawn in the
  flow beside the pager. Which size is in force is *not* drawn from that name, nor from a rule on
  `aria-pressed`: the button for the size in force wears Stiler's `button-square--secondary` and the
  other two wear `button-square--ghost`, the same filled-and-ghost pair the facet values and the
  sort buttons already use. So a host with Stiler and nothing else shows the current size correctly
  without owing this package a stylesheet, which is the whole reason the state is carried by a class
  swap rather than by an attribute selector.
- The three buttons carry Stiler's own `margin-right`, which is what keeps them apart: Razor drops
  the whitespace between elements, so without it they would touch. Both sample hosts style
  `munin-explorer-pagination-size` as a flex row and give the group's label a right margin.
- Deliberately not a `<select>`, although Runa's own control is one and it would have been less
  markup. No class name for a select can be read back off Stiler — helsedata's pager has no size
  control, so there is nothing to copy and anything chosen would be invented, and an unstyled select
  inside an otherwise styled page is the failure this package exists to avoid. Deliberately not a
  `radiogroup` either: that role's single tab stop and arrow-key navigation need script, and this
  package ships none. (Fhi.Metadata-nd2q3)
