category: Notes for hosts
- **`MuninExplorer:Features:ListLink` turns on the own-list link, `?listeId=`.** Off by default. The id is the one
  `GET api/explorer/my/lists` gives the list; the README, under «Turning features on per environment», has the link
  format. Once the list is open or found missing the explorer drops `listeId` from the address; signed out it keeps it,
  so make sure the host's sign-in returns the reader to the address they came from. With the flag off the key is
  not read and is left in the address untouched. A host whose page already means something by `listeId` can decline
  it through `DeclinedKeys`. `ExplorerUrlState.QueryKeys` and `ScalarQueryKeys` now include `listeId` whatever the
  flag says, and `ExplorerUrlState.Parse` reads it, so a host that uses `ExplorerUrlState` itself and has a parameter
  of its own by that name needs to rename it.
