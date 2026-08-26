category: Changed

- **`IMuninExplorerClient` gained `GetKildePropertyMetadataAsync`** - the vocabulary behind the
  curated properties the kilde list carries, over `api/explorer/kilder/egenskaper`, as the same
  `PropertyMetadataEntry` list the detail endpoints ship with a record. It is a sibling of the list
  rather than a field on it because the vocabulary is one definition per property and not one per
  kilde. Not breaking: it is the one member on the interface with a default implementation, which
  answers an empty list, so a host that implements the interface itself keeps compiling and its
  kategori and tilgangsnivå facets show the catalogue's own tokens instead of words - the same
  degradation as the endpoint being unreachable. Overriding it is what turns those tokens back into
  words. It takes no language, deliberately, since the entries carry every label in `OptionsJson`
  and the caller picks per render. (Fhi.Metadata-tbpbr)
