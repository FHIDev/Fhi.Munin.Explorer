category: Fixed

- **Pressing a kilder row opens its datasamlinger, as pressing the chevron does.** The row already
  lit up under the pointer and had no handler behind it, so it looked like a control and did
  nothing when pressed. It now opens the same drawer the expand chevron opens, and only where
  there is something to open — a kilde with no datasamlinger has no chevron and the row stays
  inert. The controls inside the row keep their own jobs: the name still opens the kilde, and the
  selection box still only ticks. (Fhi.Metadata-l9l2n.55)
