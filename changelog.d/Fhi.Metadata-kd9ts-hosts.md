category: Notes for hosts
- **The variable explorer's filter toolbar now lays out four controls, not three.**
  `munin-explorer-filters__toolbar` holds Utvid alle, Skjul alle, Nivålinjer and now Ikoner, and
  the second switch is a second member at its own natural width: the `min-width: 0` half of the
  rule must keep selecting the two fold buttons alone, because with it the switch collapses to one
  character wide. Nothing to add if you took `Fhi.Helsedata.Stiler`'s rule — it already selects
  `hd-button-square`, which neither switch wears — and neither sample stylesheet needs a new rule,
  for the same reason. A host that wrote a toolbar rule of its own has one more member to fit,
  and the two fold buttons are what absorbs it.
  No new class name comes with the switch: it wears `munin-explorer-switch` with
  `munin-explorer-switch__track` and `munin-explorer-switch__thumb` inside it, exactly as
  Nivålinjer does, so a host already drawing that control draws this one. A host on a published
  Stiler still draws none of the three itself — that release is `Fhi.Metadata-aonvl` — and now has
  two bare `<button>`s whose appearance never changes with their state rather than one.
  (Fhi.Metadata-kd9ts)
