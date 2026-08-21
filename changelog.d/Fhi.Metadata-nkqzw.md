category: Fixed
- **The whole-variable view's block headings wore `headline-sm`, a name no stylesheet defines** -
  Eight headings in `VariableView` — Metadata, Versjonshistorikk, Statistikk, Kildeinformasjon,
  Dataperiode, Datatype, Variabelgrupper and Datasamlinger — plus the Kodeverk heading the detail
  panel contributes to that view carried `headline headline-sm`. Neither Stiler nor any of
  helsedata's seven bundles has a rule for `headline-sm`, so those nine headings fell back to the
  browser's own `<h*>` size inside an otherwise styled page. They now wear `headline-s`, the same
  size the view's own name wears, matching the fix `KildeView` got under `Fhi.Metadata-e4bj2` —
  that round swept the kilde view only. A host needs no change. (Fhi.Metadata-nkqzw)
