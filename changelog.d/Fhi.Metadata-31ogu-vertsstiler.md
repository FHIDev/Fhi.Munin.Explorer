category: Notes for hosts
- One new class name, `munin-explorer-alert`, the row holding a failure and the control that
  answers it. A host without a rule for it gets what it had before — the message and the button
  stacked — so this is a name a host owes rather than one it breaks without. Both sample hosts
  draw it as a wrapping flex row with a 16px gap.
- It also needs `.munin-explorer-alert .infobox { margin: 0 }`. Stiler centres an infobox in its
  column with `margin: auto`, and inside a flex row an auto margin eats the free space and pushes
  the button off the end of it. The rule belongs beside the row's own in
  `components/munin-explorer/`.
- The two retry buttons wear `button-square--secondary` where they wore `button-square--ghost`,
  which is Stiler's own filled pair and the one Tøm søket already uses. No new name, and nothing
  further owed for it. (Fhi.Metadata-31ogu)
- `aria-busy="true"` appears on the failure box while the retry it offered is running. Both sample
  hosts draw a gradient wave across the box from it, behind a `prefers-reduced-motion: reduce`
  guard, since a moving gradient is what WCAG 2.3.3 asks to be able to turn off. A host that styles
  nothing for it loses only the wave: the words in the box already say a retry is running.
