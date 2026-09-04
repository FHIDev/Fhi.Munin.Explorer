category: Added

- **The kilde table has a column picker**, the one Munin's own kildeutforsker has. Ten optional
  columns in its order — Kildetype, Datasamlinger, Variabler, Delkilder, Dataansvarlig,
  Databehandler, Grad av personidentifikasjon, Gyldighet, Importert and Sist endret — of which the
  first three start on, which is the table hosts already have. Navn, Status and Opprettet are
  outside the picker's reach, so no choice can empty a row. The choice is not persisted and is not
  in the URL: it lasts as long as the page, exactly as it does in the kildeutforsker.
- **Seven of those columns are new data on this table**, and two of them are dates the payload
  spells confusingly: `Importert` is `opprettet`, Munin's own row timestamp, while the
  always-visible `Opprettet` column stays the founding year the catalogue states in
  `additionalProperties.Opprettet`; `Sist endret` is the catalogue's own
  `additionalProperties.SistOppdatert`, not `sistOppdatert`, which is Munin's. A source date the
  catalogue did not write as `yyyyMMdd` is shown exactly as it stands rather than blanked or
  guessed at. (Fhi.Metadata-ay3zz)
