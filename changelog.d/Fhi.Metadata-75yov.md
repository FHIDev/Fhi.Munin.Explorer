category: Added
- **A reader can mark individual datasamlinger across several kilder in `KildeExplorer` and
  explore the variables in exactly those.** Every expanded row's datasamling table gains a leading
  checkbox column, each marked row shows a count under its kilde's name, and the selection bar
  counts the marks beside the ticked kilder. The marks ride in the address as a repeated
  `?selectedDatasamling=<kildeId>:<datasamlingId>`, so they survive going back and forth between
  the two explorers and are gone with the link — nothing is kept in `sessionStorage` or
  `localStorage`. With any mark present the handover travels as `datasamlingIds` alone, each
  ticked kilde expanded to all of its own datasamlinger: Munin's API ANDs `kildeIds` with
  `datasamlingIds`, so sending both would drop every variable pinned into another kilde's
  datasamling. A ticked kilde holding no datasamling is named in a note beside the button, which
  also says that variables in no datasamling are out of reach of such a selection. A selection
  with no marks is unchanged. (Fhi.Metadata-75yov, sak #6098)
