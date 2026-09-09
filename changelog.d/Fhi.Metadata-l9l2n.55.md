category: Fixed

- **Pressing a kilder row opens its datasamlinger, as pressing the chevron does.** On helsedata.no
  the row already lit up under the pointer and had no handler behind it, so it looked like a
  control and did nothing when pressed. It now opens the same drawer the expand chevron opens, and
  only where there is something to open — a kilde with no datasamlinger has no chevron and the row
  stays inert. Selecting text in a row is not a press: a drag that begins and ends inside the row
  lands a click on the row too, so a pointer that travels more than a few pixels in either
  direction between press and release leaves the drawer alone, and so does a shift-click extending
  a selection onto the row. A double-click, which is how a short code like K_ALS is taken, opens
  the drawer once instead of flashing it open and shut under the selection being made and asking
  the catalogue twice for the same kilde — so the code under the name stays copyable. The controls
  inside the row keep their own jobs: the name still opens the kilde, and the selection box still
  only ticks. (Fhi.Metadata-l9l2n.55)
