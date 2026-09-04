category: Fixed

- **A kilde or datasamling whose payload carries `"sistOppdatert": null` no longer fails to load
  at all.** The property was non-nullable, so an explicit null threw inside deserialisation and the
  component reported "Kunne ikke hente datakilden" — the reader lost the whole panel over one
  field. Munin declares these columns nullable and already sends explicit nulls for dates
  elsewhere, so this was reachable from the live API. An absent key and an explicit null now mean
  the same thing: the field is simply not shown. (Fhi.Metadata-se0by)
