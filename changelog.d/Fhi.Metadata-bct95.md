category: Changed
- **A property Munin keeps in a column of its own is drawn in the section the catalogue places it
  in.** Beskrivelse, Lovverk, Dataansvarlig, Databehandler, GradAvPersonidentifikasjon, GyldigFra
  and GyldigTil — and, on a datasamling, StatistikkType, TelleEnhet and Frekvens — arrive as typed
  fields rather than in the `additionalProperties` bag, so the metadata sections had no value to
  draw and a section holding only such keys came out empty. The kilde, datasamling and variable
  views now merge those columns into the values they resolve properties from, and each fact box row
  that would repeat one yields to the section that draws it, so a fact still appears exactly once.
  A page reading an API whose placements have not been seeded is unchanged: the fact box keeps
  drawing every one of them. Gyldighet is the one row the catalogue splits in two, so it yields per
  end: with GyldigFra placed and GyldigTil not, the box drops the period and keeps the unplaced end
  under its own label, leaving neither date undrawn. (Fhi.Metadata-bct95)
