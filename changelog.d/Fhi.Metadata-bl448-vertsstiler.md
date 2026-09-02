category: Notes for hosts
- **The account-link panel adds two class names a host has to provide,
  `munin-explorer-account-link` and `munin-explorer-account-link__actions`.** Neither is in
  `Fhi.Helsedata.Stiler` today and neither carries state: the panel is the box that hangs under
  the "Koble konto" trigger, and the second is the row its two buttons sit in. Undefined, both
  still work — the panel renders in the flow of the actions row instead of floating over the
  results, and the buttons stack instead of sitting side by side. That is cosmetic rather than
  misleading, which is why this is a note and not a defect, but a panel that widens the header
  row is visibly not what was intended. Both sample hosts show a working approximation: absolute
  under the trigger at `top: 36px`, the same offset the choicepicker beside it uses.
  (Fhi.Metadata-bl448)
- **Everything inside the panel wears a name Stiler already defines.** The label is
  `form-element__label`, the code field is `searchbox__freetext` — the search box's own input —
  and the four buttons are `hd-button-square` with `button-square--secondary` or
  `button-square--ghost`. Nothing there needs a new rule, which is deliberate: an unstyled text
  input inside an otherwise styled page is the failure this package exists to avoid, so the field
  borrows rather than inventing. (Fhi.Metadata-bl448)
