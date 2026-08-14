category: Notes for hosts
- Every request the client makes carries `X-Munin-Explorer-Client: blazor/<version>`. Munin's API
  is anonymous, and this is how it tells embedded-component traffic apart from anything else.
- A host that implements `IMuninExplorerClient` itself has seven new members to fill in. While on
  `0.x` the interface still moves; a component only calls what it needs, so unimplemented members
  can throw.
