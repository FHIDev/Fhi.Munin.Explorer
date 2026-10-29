category: Notes for hosts
- **`IMuninExplorerClient` gains `ShareMyListAsync(Guid id)`, and the list view now calls it.** A host
  that consumes `MuninExplorerClient` has nothing to do. A host with its own `IMuninExplorerClient` gets
  the interface's default, which throws `NotSupportedException`, so «Del liste» shows its error until the
  member is implemented. `ShareListAsync` stays, for a list that is not saved.
