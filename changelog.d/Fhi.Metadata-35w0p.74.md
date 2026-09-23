category: Notes for hosts
- **Every kilder table header cell now carries a `munin-explorer-kilder-header__<key>` handle.**
  This mirrors `munin-explorer-dataitem-header__<key>` in the variable explorer. The thirteen
  optional columns use their `KildeSearch.ColumnKeys` key: `munin-explorer-kilder-header__kode`,
  `__kildetype`, `__datasamlinger`, `__variabler`, `__delkilder`, `__dataansvarlig`,
  `__databehandler`, `__grad`, `__gyldighetsperiode`, `__importert`, `__sistEndret`,
  `__andelKodeverk` and `__andelStatistikk`. The fixed cells use `__navn`, `__status`, `__opprettet`,
  `__expand` and `__select`, and cells keep the classes they already had. These are handles, so a
  host with no rule for them loses nothing. They exist so a stylesheet can key the sticky head's
  thresholds on which wide columns are shown. `munin-explorer-kilder-scroll--cols-N` counts columns
  but cannot tell the default three (772px) from Kode, Dataansvarlig and Databehandler (998px).
  The matching `Fhi.Helsedata.Stiler` rules are `Fhi.Metadata-35w0p.75`. (Fhi.Metadata-35w0p.74)
