category: Notes for hosts
- **Three new class names, and a toolbar rule that must not reach them.** `munin-explorer-switch` is
  the Nivålinjer control, `munin-explorer-switch__track` and `munin-explorer-switch__thumb` are the
  two empty spans inside it that draw the on/off state. `Fhi.Helsedata.Stiler` carries all three
  from the release that follows PR 39257; a host on an older one, or on no Stiler at all, gets a
  browser-default `<button>` with its label — operable and named, since the state is announced from
  `aria-checked` rather than drawn — but with no visible on/off mark, so a sighted reader loses the
  state a screen reader still hears. Both sample stylesheets show the shape: a 30×18 track and a
  12×12 thumb travelling 12px, keyed on `[aria-checked="true"]` and never on a modifier class, with
  both track colours over WCAG 1.4.11's 3:1 against the page ground. **The trap is the toolbar
  rule.** `munin-explorer-filters__toolbar > .hd-button-square` sets `min-width: 0` and
  `overflow-wrap: anywhere`, and the switch deliberately no longer wears `hd-button-square` so that
  rule cannot reach it. A host whose own toolbar rule selects the row's children instead of that
  class will reach it: measured on the hostile host, the same markup under those two declarations
  came out 4.72×304.34px — one character wide, every letter on a line of its own — where 114.98×32
  is what the control should draw. (Fhi.Metadata-l9l2n.87)
