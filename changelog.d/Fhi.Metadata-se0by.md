category: Changed

- **Munin's own timestamps on the contracts are now `DateTimeOffset?`** — `opprettet` and
  `sistOppdatert` on `KildeDetail`, `DatasamlingDetail` and `KildeSummary`, and `createdAt`,
  `updatedAt` and `addedAt` on the variable-list types. A host that reads one of them into a
  non-nullable local will not compile until it handles the absence; nothing else about the wire
  format or the rendering changes. (Fhi.Metadata-se0by)
