category: Notes for hosts

- **The two static blocks over an open kilde are now off by default.** "Kriterier for tilgang til
  data" and "Priser" draw only when the host sets `ShowAccessAndPrices="true"`, which is declared
  on both of Kelda's mounts — `KildeSearch` and `KildeExplorer`. Both blocks send
  the reader to helsedata.no, so a host embedded on a site that already publishes its own access
  and pricing pages was shipping a second copy of content it does not own, inside a component it
  cannot edit. **A host mounting the kildeutforsker on its own site has to add the parameter to
  keep the blocks it had.** Nothing else on the kilde page moves with it: the variable count, the
  metadata, the datasamlinger and the sidebar are drawn either way. (Fhi.Metadata-ay3zz)
