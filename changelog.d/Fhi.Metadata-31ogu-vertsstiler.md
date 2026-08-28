category: Notes for hosts
- One new class name, `munin-explorer-alert`, the row holding a failure and the control that
  answers it. A host without a rule for it gets what it had before — the message and the button
  stacked — so this is a name a host owes rather than one it breaks without. Both sample hosts
  draw it as a wrapping flex row with a 16px gap.
- It also needs `.munin-explorer-alert .infobox { margin: 0; flex: 1 1 auto }`. Stiler centres an
  infobox in its column with `margin: auto`, and inside a flex row an auto margin eats the free
  space and pushes the button off the end of it. `flex: 1` is the other half: the sentence changes
  while a retry runs, and a box sized to its own words would move the button left and right under
  the reader's pointer. Filling the row up to Stiler's existing 720px cap keeps it still. The rule
  belongs beside the row's own in `components/munin-explorer/`.
- The two retry buttons wear `button-square--secondary` where they wore `button-square--ghost`,
  which is Stiler's own filled pair and the one Tøm søket already uses. No new name, and nothing
  further owed for it. (Fhi.Metadata-31ogu)
- `aria-busy="true"` appears on the failure box while the retry it offered is running. Both sample
  hosts draw a gradient wave across the box from it, behind a `prefers-reduced-motion: reduce`
  guard, since a moving gradient is what WCAG 2.3.3 asks to be able to turn off. A host that styles
  nothing for it loses only the wave: the words in the box already say a retry is running.
- **The inert rule for `munin-explorer-retry` in Stiler must gain a background.** It currently sets
  `color: var(--grey60)` and nothing else, which was right while these buttons were ghosts and is
  wrong now they are `button-square--secondary`: that is grey60 text on a grey60 background, a
  caption nobody can read until a hover changes the background under it. The pair the pager already
  uses is the fix — `background-color: var(--grey30); color: var(--grey60)`, on both the base and
  the `:hover` — because the pager's buttons are secondary too. Both sample hosts carry it.
  Until Stiler ships it, a Stiler-only host draws a retry button whose words are invisible while it
  is inert, which is worse than the state `Fhi.Metadata-x6vqc` fixed. Neither guard here can catch
  that: both ask whether a name has a rule, not which declarations the rule carries.
