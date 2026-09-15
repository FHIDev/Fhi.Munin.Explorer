category: Added
- **The three detail views open with a row of six facts under the name.** `DetailPage` draws it
  between the name block and the body, so the kilde, datasamling and variable views get it at once.
  Each fact is a label, a value and an optional second line carrying the qualifier that makes the
  value honest — `630 variabler`, then `i 6 datasamlinger`. The second line is left out rather than
  drawn empty when there is nothing to qualify, and a fact the catalogue has not filled in is
  dropped rather than drawn blank, so the row is a summary of what the record actually has.
  (Fhi.Metadata-35w0p.26)
- **`DetailPage` takes a `Facts`, and `DetailFact` is what goes in it.** A label, a value and an
  optional note, each with its own `lang` for the half that is the catalogue's Norwegian: a value
  and its note are usually in different languages, so they are marked apart. Empty or unset draws
  no row at all. (Fhi.Metadata-35w0p.26)
- **A source and a datasamling lead with the same six facts, and a variable with six of its own.**
  Kildetype, Dataansvarlig, Grad av personidentifikasjon, a period, a count and Lovverk on the two
  entity pages — Dataperiode and the total variable count on a source, Gyldighet and the
  collection's own count on a datasamling. A variable leads with Kodeverk, Statistikk, Opprinnelse,
  Identifiseringsgrad, Databasereferanse and Dataperiode, and with neither Kilde nor Datasamling:
  the breadcrumb directly above already names both. **Every one of them is still drawn in its
  section below** — the row summarises the page rather than moving anything out of it, and each
  value is resolved through the member that section reads, so the two cannot come out in different
  words. (Fhi.Metadata-35w0p.26)
