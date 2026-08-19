category: Added
- **The kilde and the datasamling a variable belongs to now open from inside its result card** -
  "Vis datakilde" and "Vis datasamling" under an open variable panel disclose the owner's own
  record, fetched from `GetKildeAsync` and `GetDatasamlingAsync`. The kilde says what kind of data
  source it is, who controls and processes the data, at what level of personal identification, on
  what legal basis, over what period, and how many datasamlinger and variables it holds; the
  datasamling says the same for itself plus its inclusion and exclusion criteria, its frequency and
  what one row of it counts. As with the variable panel there is no navigation behind it — the
  owner is drawn inside the card, so a CMS host that owns its own routing can offer kilde and
  datasamling detail at all. (Fhi.Metadata-l9l2n.15)
- **The datasamling reads its inherited values rather than its own** - Munin lets a datasamling take
  its data controller, data processor, identification level, legal basis and validity from its
  delkilde or its kilde, leaving its own fields empty. The panel shows what actually applies, so a
  datasamling whose controller is recorded one level up no longer reads as "Ikke oppgitt".
  (Fhi.Metadata-l9l2n.15)
- **One owner at a time, and never outliving the variable it hangs in** - opening the datasamling
  replaces the kilde rather than stacking beside it, and closing the variable panel, opening another
  row, searching, filtering, reordering or turning a page takes the owner panel with it. A fetch
  that fails, or a kilde the catalogue does not publish, says so inside the owner panel and leaves
  both the variable above it and the rows around it alone. (Fhi.Metadata-l9l2n.15)
