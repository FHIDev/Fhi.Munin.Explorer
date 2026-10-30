category: Added
- **A datasamling's own Hjemmeside, organisation number and HER-IDs show in Kildeinformasjon without a caption.**
  The values come from `ownKildeProperties`, a new key on Munin's datasamling detail, and win over an
  inherited value of the same key, which keeps its «Arvet fra …» caption. A Hjemmeside becomes a link only for an
  http(s) or mailto address, as the inherited one does. A response without the key draws what it drew before.
