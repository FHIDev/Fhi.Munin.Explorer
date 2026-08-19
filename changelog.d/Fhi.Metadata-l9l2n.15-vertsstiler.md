category: Notes for hosts
- **The kilde and datasamling panel adds a fourth handle and no style names** -
  `variable-explorer-source` joins `variable-explorer`, `variable-explorer-filters` and
  `variable-explorer-detail`, and carries no styling in this package or in Stiler. The panel itself
  is a heading and a `<dl>` wearing Stiler's own `datasourcecard__heading` and
  `form-element__label`, opened by the same ghost `hd-button-square` the sort, facet and detail
  controls already use — so a host that has styled the variable detail panel has very nearly styled
  this one. What is worth adding is the inset that says the kilde sits *inside* the variable rather
  than beside it; both sample hosts show one. (Fhi.Metadata-l9l2n.15)
- **A result card can now hold a heading below the card's own** - the owner panel is headed at one
  level below the result card, which is two below the component's `HeadingLevel`. A host that sets
  `HeadingLevel` correctly gets an unbroken outline for free; a host that styles headings by element
  rather than by class should check that level. (Fhi.Metadata-l9l2n.15)
