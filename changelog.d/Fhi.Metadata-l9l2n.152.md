category: Added
- **A delkilde and the datasamling detail carry the kilde fields they inherit.** `KildeDelkilde` and
  `DatasamlingDetail` get `InheritedProperties`: the public kilde fields they leave blank, by key, each as an
  `InheritedValue` with the value and where it comes from (`SourceLevel` `"delkilde"` or `"kilde"`, `SourceId`,
  `SourceName`, `SourceNameEn`). Munin has sent it since FHIDev/Munin#6871; the package now reads it instead of
  dropping it. Nothing renders it yet. An older Munin that doesn't send it gives an empty dictionary.
  (Fhi.Metadata-l9l2n.152, Fhi.Metadata-l9l2n.153)
