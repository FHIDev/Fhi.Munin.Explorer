category: Notes for hosts

- **Two new class names, both in the saved-list view's new "Ønskede data" column.**
  `munin-explorer-dataitem-header__desiredData` on the column header and
  `munin-explorer-dataitem-main__desiredData` on the cell, which is the one cell in the component
  that holds an editable field rather than a value. `Fhi.Helsedata.Stiler` carries no rule for
  either yet, and this repository's CI cannot see Stiler, so a green build here is not evidence
  the column is drawn. (Fhi.Metadata-m74i4)
- **What an undefined pair costs, and what the rule owes.** Undrawn, the field is a browser-default
  text box: visible, operable and named, so nothing is lost but the column's width and the mark on
  a refused text. Two things the rule does owe when it is written. The field's own border is what
  says a field is there, which makes it a non-text control indicator under WCAG 1.4.11 and owes
  3:1 against whatever the row sits on — `--grey30`, which every other border in the sample
  stylesheet uses, measures 1.16:1 and is invisible on a bright desktop, so both samples use
  `--grey60`. And `input[aria-invalid="true"]` is the state marking the row the API refused; the
  samples thicken the border as well as colouring it, because 1.4.1 does not accept a hue as the
  whole signal. The refusal is a sentence in the component's alert region either way, so a host
  that draws neither loses the mark and not the reason.
