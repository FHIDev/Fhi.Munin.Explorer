category: Added

- **The panel's third group, Egenskaper, showing the catalogue's own properties** - Opprinnelse,
  Kommentar, Datatype, Identifiseringsgrad, Databasereferanse, Erstatter and Synlig, which is Runa's
  set. Coded values are resolved to words: "Opprinnelse: 5" now reads "Direkte fra skjema", and
  "Synlig: 1" reads "Ja".
- **Nothing about those properties is known to this package** - which keys exist, what they are
  called, what order they come in and what their codes mean all arrive with the payload, in the
  reader's language. A property added or renamed in Munin appears here without this package being
  touched, and no vocabulary is copied into it — a copy would freeze editable master data in one
  language and drift the first time someone edited a definition. A key the catalogue no longer
  describes is skipped rather than drawn under its raw name, and a malformed vocabulary costs that
  one field its label rather than taking the panel down. (Fhi.Metadata-88tyl)
