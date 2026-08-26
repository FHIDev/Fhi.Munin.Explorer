category: Changed

- **`IMuninExplorerClient` gained `GetKildePropertyMetadataAsync`** - the vocabulary behind the
  curated properties the kilde list carries, over `api/explorer/kilder/egenskaper`, as the same
  `PropertyMetadataEntry` list the detail endpoints ship with a record. It is a sibling of the list
  rather than a field on it because the vocabulary is one definition per property and not one per
  kilde. Breaking for a host that implements the interface itself: that implementation needs the
  new method, and answering an empty list from it is a working answer - the facets then show the
  catalogue's own tokens instead of words. It takes no language, deliberately, since the entries
  carry every label in `OptionsJson` and the caller picks per render. (Fhi.Metadata-tbpbr)
