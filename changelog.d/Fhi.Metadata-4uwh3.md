category: Added

- **A row can be saved to the reader's variable list, and taken out again** - one control in two
  states beside the variable name, carrying `aria-pressed` so a screen reader is told the same fact
  the word shows. Signed out there is no button at all rather than a disabled one: a control that
  can never do anything is worse than no control, and the state holder would refuse the call anyway.
  (Fhi.Metadata-4uwh3)
- **Whether a variable is saved is read from the circuit's state holder on every render, never
  remembered by the row.** The results are rebuilt whenever the facet counts change, so a button
  that kept its own answer would forget it at the next refiltering and then show "Lagre i liste" for
  a variable that is in the list.
- **A reader who has no list yet gets one when they first save**, named "Min variabelliste". That is
  helsedata's 118497, and it is the same action as 118721 rather than a separate one: refusing to
  save until the reader had made a list somewhere else would make the button lie about what it does.
- **The button wears Stiler's `hd-button-square` and no `munin-explorer-*` name of its own.** The
  package ships no CSS, so a name invented here would be one with no rule behind it — it would
  render unstyled in the host until somebody wrote the rule in Stiler. The class-name guard is
  asserted with the button in both of its states.
