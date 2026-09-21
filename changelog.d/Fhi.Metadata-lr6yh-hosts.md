category: Notes for hosts
- **Deep links into a datasamling page's metadata section change where the catalogue has placed its
  properties: `#metadata` becomes one `#section-<section key>` per placed section.** The keys are
  Munin's, not this package's — `#section-om-datasamlingen`, `#section-datakilde`,
  `#section-alle-metadatafelt` and whichever others the placement rows declare — so the set is open
  and a host that writes ids of its own should not start one `section-`. This is the same family a
  kilde page has written since `Fhi.Metadata-35w0p.22`, not a second one. Bare `#metadata` has not
  gone: it anchors whatever the catalogue titled but placed nowhere, so a payload that places some
  of its groups and not others writes that id and the new ones on the same page, and a payload
  predating the placement rows writes it alone. The three other ids a datasamling page writes are
  unchanged: `criteria`, and `source` and `statistics` for as long as the catalogue has placed
  nothing those blocks draw. No class name is added or renamed, so no `Fhi.Helsedata.Stiler` rule is
  needed for this. (Fhi.Metadata-lr6yh)
