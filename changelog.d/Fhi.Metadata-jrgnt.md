category: Fixed
- **The whole-variable page no longer draws an empty bullet for a variabelgruppe the catalogue
  left unnamed.** Its Variabelgrupper list read the payload's own list while the Datasamlinger
  list below it asked a shared predicate, so an unnamed group drew a bullet with nothing beside
  it, and a variable whose groups were all unnamed got a heading and a contents entry over empty
  bullets. Both the list and its contents entry now come from one predicate, which drops the
  unnamed groups and falls back to the primary group's name when that leaves none — the reading
  the drill-in panel already had, now shared rather than copied. (Fhi.Metadata-jrgnt)
