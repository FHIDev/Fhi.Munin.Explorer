category: Added
- **`PropertyMetadataEntry` carries `GroupKey`, and `KildeSummary` carries `KodeverkShare` and `StatisticsShare`.**
  The Explorer API now sends all three. `GroupKey` is the stable id of a property's section (for
  example `om-registeret`) and is null where the group has no key. The two shares are percentages
  of a kilde's visible variables with a kodeverk link or a statistikk entry, and are null when the
  kilde has no visible variables. Nothing in the components reads them yet, and all three are
  nullable, so a host on an older API is unaffected.
