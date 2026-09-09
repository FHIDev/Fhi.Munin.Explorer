category: Fixed

- **Pressing a kilder row opens its datasamlinger, as pressing the chevron does.** On helsedata.no
  the row already lit up under the pointer and had no handler behind it, so it looked like a
  control and did nothing when pressed. It now opens the same drawer the expand chevron opens, and
  only where there is something to open — a kilde with no datasamlinger has no chevron and the row
  stays inert. Selecting text in a row is not a press: a drag that begins and ends inside the row
  lands a click on the row too, so a pointer that travels more than a few pixels between press and
  release leaves the drawer alone and the code under the name stays copyable. The controls inside
  the row keep their own jobs: the name still opens the kilde, and the selection box still only
  ticks. (Fhi.Metadata-l9l2n.55)
