category: Added
- **The datasamling page draws Opprettet i Munin and Statistikktype, and its parent kilde can be
  a link.** All three facts were already on the payload and rendered nowhere. "Opprettet i Munin"
  sits beside "Sist oppdatert i Munin" in Kildeinformasjon, worded that way because the bare
  "Opprettet" is Kelda's column for the founding year the import file states. Statistikktype is a
  row in the statistics block, resolved through the same vocabulary that names the heading over it
  and yielding to a section the catalogue has placed the key in, as Frekvens and Telleenhet
  already do. `DatasamlingView` gains a `KildeHref` parameter — `Func<Guid, string>?`, given the
  owning kilde's id and answering an address — which makes the Kilde row the way back up to the
  source; left unset, as it is everywhere but the kildeutforsker, the name is plain text rather
  than a link that goes nowhere. No new class name. (Fhi.Metadata-35w0p.50)
