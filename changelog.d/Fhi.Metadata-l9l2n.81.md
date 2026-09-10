category: Fixed

- **Pressing a variabelutforsker row opens its panel, as pressing the variable's name does.** On
  helsedata.no the row's column strip already computed `cursor: pointer` and had no handler behind
  it, so it looked like a control and did nothing when pressed — the same complaint that made the
  kildeutforsker's rows pressable. The name keeps being the disclosure: it is the button that
  carries `aria-expanded`, it is what the panel is named after, and Runa gives it no second
  destination to be freed up for, so the row is a pointer shortcut onto it and adds no tab stop.
  Selecting text in a row is not a press: a drag that begins and ends inside the row lands a click
  on it too, so a pointer that travels more than a few pixels in either direction between press and
  release leaves the panel alone, and so does a shift-click extending a selection onto the row. A
  double-click, which is how a short code is taken, opens the panel once instead of flashing it
  open and shut under the selection and asking the catalogue twice for the same variable. The
  controls inside the row keep their own jobs: the name still toggles exactly once, and "Lagre i
  liste" still only saves. (Fhi.Metadata-l9l2n.81)
