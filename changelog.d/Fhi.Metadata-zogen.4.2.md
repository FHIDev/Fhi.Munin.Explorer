category: Added
- **A datasamling's own Hjemmeside, organisation number and HER-IDs show in Kildeinformasjon without a caption.**
  The values come from `ownKildeProperties`, a new key on Munin's datasamling detail, read into the new
  `DatasamlingDetail.OwnKildeProperties`. Where the datasamling holds a field itself, its own value is drawn in place
  of an inherited one; a field it only inherits keeps its «Arvet fra …» caption. A Hjemmeside becomes a link only
  for an http(s) or mailto address, as the inherited one does. A response without the key draws what it drew before.
