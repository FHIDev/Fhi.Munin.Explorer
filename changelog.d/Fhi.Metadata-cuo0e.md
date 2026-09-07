category: Notes for hosts

- **`munin-explorer-kilder__expand` needs a rule that out-specifies the host's own cell rule, and
  less side padding than the rows carry.** In `Fhi.Helsedata.Stiler` it is written scoped —
  `.munin-explorer-kilder .munin-explorer-kilder__expand` — because a stylesheet that pads `th, td`
  under the table's class is (0,1,1) and a bare class name loses to it. A host that writes the rule
  bare gets a left-aligned column that keeps the rows' 12px of side air, and at that padding the
  column is content-driven, so `width: 32px` does nothing either. Measured in the sample: 46.95px
  wide with the glyph flush left, against 32px and centred once the rule is scoped and the padding
  is 4px. (Fhi.Metadata-cuo0e)
