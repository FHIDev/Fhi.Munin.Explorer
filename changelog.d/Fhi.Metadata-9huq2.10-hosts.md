category: Notes for hosts
- **This version needs a Munin with `POST api/explorer/my/lists/{id}/share` (Munin #6956, Fhi.Metadata-9huq2.6).**
  Against an older Munin API the route answers 404, and «Del liste» shows its error for every saved list.
  Move a host's pin only once the Munin it calls serves the route. `IMuninExplorerClient` gains
  `ShareMyListAsync(Guid id)`, which the list view now calls: a host that consumes `MuninExplorerClient` needs
  no code change, while one with its own `IMuninExplorerClient` gets the interface's default, which throws
  `NotSupportedException`, until it implements the member. `ShareListAsync` stays, for a list that is not saved.
