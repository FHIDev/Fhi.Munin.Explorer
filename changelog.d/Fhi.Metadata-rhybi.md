category: Fixed

- **`KildeExplorer` no longer heads the datasamling section "Delkilder og datasamlinger" on a source
  that has no delkilder.** It passed that word over every source it opened, so on 61 of the 66
  sources the API serves the heading named something the section did not draw. It now passes no
  heading and takes `KildeView`'s default, which reads the source: "Delkilder og datasamlinger" when
  there are delkilder, "Datasamlinger" when there are none. `VariableExplorer` already behaved this
  way, so the two explorers now head the same source with the same word. A host that wants a word of
  its own still sets `DataCollectionsHeading` on `KildeView`. (Fhi.Metadata-rhybi)
