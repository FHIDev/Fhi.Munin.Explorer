category: Fixed

- **The variabelutforsker's filter panel and its active-filter chips now say which language their
  words are in.** Every chip over the results passed no language at all, so a screen reader on an
  English page announced "Tromsøundersøkelsen" with English phonetics (WCAG 3.1.2) — the marking
  the kildeutforsker's own chips and facet labels have carried from the start. A chip drawn from a
  catalogue value now carries `lang` on those words alone, never on the capsule or on the remove
  control whose name is this package's prose, and the checkbox that chip stands for carries the
  same marking, so one kilde is never named two ways on one page. Words that are this package's
  own — a kildetype, a datatype, a level's fallback word, the catch-all's yes/no filters — carry
  none, and neither does a token belonging to no language: a datakategori CURIE, an OID, a code,
  or a helsefaglig kodeverk's short name, which is the catalogue's key and holds ICD-10 beside
  DÅR. Both sweeps that build the row decide it the same way, so one kilde's chip cannot differ
  from another's on nothing but which sweep drew it. (Fhi.Metadata-o49mx)
