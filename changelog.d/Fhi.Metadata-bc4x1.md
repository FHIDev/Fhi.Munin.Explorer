category: Changed

- **`KildeExplorer`'s table shows the columns Munin's own Kelda shows by default** - Navn,
  Kildetype, Status, Datasamlinger, Variabler and Opprettet, in Kelda's order. Dataansvarlig,
  Databehandler and Delkilder are gone from it: Kelda keeps all three behind its column picker,
  off by default, and a reader comparing the two side by side was looking at two different
  tables. (Fhi.Metadata-bc4x1)
- **Opprettet is the kilde's founding year, not when Munin registered it** - it comes from
  `KildeSummary.AdditionalProperties["Opprettet"]` and is shown exactly as the catalogue wrote
  it, since the source holds values like `2916`, `1900` and `0` that a date formatter would
  blank or misread. `KildeSummary.Created` is the other fact - Munin's own row timestamp, which
  Kelda draws as Importert and keeps off by default - and no column is bound to it.
  (Fhi.Metadata-bc4x1)
