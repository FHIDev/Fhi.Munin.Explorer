category: Added

- **Kelda's kilde view has the sections Runa's has not** - opening a kilde in `KildeExplorer` now
  draws Variabler, Kriterier for tilgang til data and Priser after the catalogue's metadata, beside
  the datasamling section it already headed "Delkilder og datasamlinger". They are markup in the
  explorer's own file, handed to the shared `KildeView` through its `Sections` slot, so that view
  still cannot tell which explorer is rendering it and Runa's kilde page is unchanged. A host's own
  `Sections` are placed after Kelda's rather than instead of them. (Fhi.Metadata-2fomm.2)
- **The two static blocks say one sentence each for now** - the access criteria and the prices are
  markdown with links out to helsedata.no and fhi.no in Munin's own Kelda, and whether they belong
  at all in a component embedded on helsedata.no is still open (`Fhi.Metadata-ay3zz`). Until that is
  answered each section carries a single plain sentence, because a heading with nothing under it
  reads as a rendering fault. (Fhi.Metadata-2fomm.2)
