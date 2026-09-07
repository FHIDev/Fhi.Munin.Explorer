category: Fixed

- **Creating a variable list no longer reads the list being left.** Two unawaited reads for the
  outgoing list used to reach the API on every create, spending part of the per-address rate
  limit's shared budget for nothing - their answers were already discarded. (Fhi.Metadata-7x62u)
