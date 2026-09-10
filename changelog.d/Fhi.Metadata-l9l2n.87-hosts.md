category: Notes for hosts
- **Three new class names, and a toolbar rule that must not reach them.** `munin-explorer-switch` is
  the Nivålinjer control, `munin-explorer-switch__track` and `munin-explorer-switch__thumb` are the
  two empty spans inside it that draw the on/off state. `Fhi.Helsedata.Stiler` carries all three
  from the release that follows PR 39257 — the rules are on `main` there but no version has shipped
  them yet, so pinning a published Stiler today gets a browser-default `<button>` with its label.
  That release is tracked as `Fhi.Metadata-aonvl`; until it lands, the only run that measures the
  control that ships is `STILER_FROM_SOURCE=1 ./scripts/check-hostile-host.sh`, which builds Stiler
  `main` from a checkout instead of restoring the pin.
  That is operable and named, since the state is announced from `aria-checked` rather than drawn,
  but it has no visible on/off mark, so a sighted reader loses the state a screen reader still
  hears. Both sample stylesheets show the shape Stiler draws: a 30×18 track and a 12×12 thumb
  travelling 12px, keyed on `[aria-checked="true"]` and never on a modifier class. Read the off
  state's colours before rewriting them — the track's `--grey20` fill is the same colour as the
  control's own hover surface and vanishes on it, so what carries WCAG 1.4.11's 3:1 off is the
  `--grey60` border, not the fill. **The trap is the toolbar rule.**
  `munin-explorer-filters__toolbar > .hd-button-square` sets `min-width: 0` and `overflow-wrap:
  anywhere`, and the switch deliberately no longer wears `hd-button-square` so that rule cannot
  reach it. A host whose own toolbar rule selects the row's children instead of that class will
  reach it, and the cost is measured rather than guessed — on the Stiler side of this pair, against
  Stiler source rather than against this repository's fixture, the same markup under those two
  declarations came out 4.72×304.34px, one character wide with every letter on a line of its own,
  where 114.98×32 is what the control should draw. (Fhi.Metadata-l9l2n.87)
