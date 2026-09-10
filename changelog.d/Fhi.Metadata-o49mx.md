category: Fixed

- **The variabelutforsker's active-filter chips now say which language their words are in.** Every
  chip over the results passed no language at all, so a screen reader on an English page announced
  "Tromsøundersøkelsen" and "DÅR" with English phonetics (WCAG 3.1.2) — the marking the
  kildeutforsker's own chips have carried from the start. A chip drawn from a catalogue value now
  carries `lang` on those words alone, never on the capsule or on the remove control whose name is
  this package's prose, and a chip whose words are this package's own — a kildetype, a datatype, a
  level's fallback word, the catch-all's yes/no filters — or a token belonging to no language, such
  as a datakategori CURIE or an OID, carries none. Both sweeps that build the row decide it the
  same way, so one kilde's chip cannot differ from another's on nothing but which sweep drew it.
  (Fhi.Metadata-o49mx)
