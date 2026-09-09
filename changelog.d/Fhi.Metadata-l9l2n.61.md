category: Changed

- **BREAKING for hosts: every kildetype on the contracts is `string?`, because the API sends null
  for a kilde that has none.** A kilde with no kildetype is a real state in the catalogue —
  `K_NKR-NAKKE` is one today — and the API now says so with an explicit `null` where it used to
  send the number `0`. The contract still declared a non-nullable `string`, so it told hosts
  something that was not true: a host deserialising with plain `System.Text.Json` got that null
  written straight over the `= ""` initialiser and found it wherever it first read the property,
  which on a Blazor Server host is the circuit and the page. Six properties change:
  `KildeSummary.Kildetype`, `KildeDetail.Kildetype`, `VariableDetail.KildeType`,
  `KildeFacet.KildeType`, and the `EffectiveKildetype` on `DatasamlingDetail` and on both nested
  records of `KildeDetail` — those last three are always the owning kilde's kildetype, so they are
  null exactly when it has none. **What a host must do:** handle null where it reads one of these.
  The compiler now says where, which is the point of the change; coalesce to your own word for an
  unset kildetype, as this package renders "Ikke oppgitt". `VariableSummary.KildeType` was already
  `string?` and is unchanged. Nothing changes on the page: the client's `NullAsEmptyStrings`
  modifier never covered a nullable string, so the components go on reading these through
  `Texts.KildeTypeLabel`, which has always taken a null. (Fhi.Metadata-l9l2n.61)
- **`HierarchyVariabelgruppe` gains `PresentationOrder`.** The hierarchy endpoint sends
  `presentationOrder` on a variabelgruppe as it does on the delkilder and datasamlinger above it,
  and the contract had nowhere to put it, so the curated order was dropped on the floor. `int?`,
  null when unordered, the same shape the two neighbouring records already use.
  (Fhi.Metadata-l9l2n.61)
