category: Notes for hosts
- **Deep links into a datasamling page's metadata section change where the catalogue has placed its
  properties: `#metadata` becomes one `#metadata-<section key>` per placed section.** The keys are
  Munin's, not this package's — `#metadata-om-datasamlingen`, `#metadata-datakilde`,
  `#metadata-alle-metadatafelt` and whichever others the placement rows declare — so the set is open
  and a host that writes ids of its own should not start one `metadata-`. The other four ids a
  datasamling page writes are unchanged: `criteria`, and `source` and `statistics` for as long as
  the catalogue has placed nothing those blocks draw. No class name is added or renamed, so no
  `Fhi.Helsedata.Stiler` rule is needed for this. (Fhi.Metadata-lr6yh)
