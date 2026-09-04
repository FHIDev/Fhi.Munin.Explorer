category: Fixed

- **A kilde or datasamling whose payload carries no `sistOppdatert` no longer shows "1. januar
  0001".** The DTOs declare Munin's own timestamp non-nullable, so an absent one arrived as
  `default` and the source-information block rendered the year 1 under "Sist oppdatert i Munin" — a
  date the catalogue never sent, stated as fact. The row is now dropped instead, which is what that
  block already does for every other field the payload leaves out. The kilde table's `Importert`
  column reads through the same guard now and behaves exactly as it did. (Fhi.Metadata-6r6rf)
