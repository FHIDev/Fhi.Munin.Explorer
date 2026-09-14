category: Added
- **The filter panel has a builder for the kilde → delkilde → datasamling → variabelgruppe tree,
  and nothing draws it yet.** It derives the whole tree from a single `GET /api/explorer/filters`
  answer: every variabelgruppe the answer carries whatever the API's own facet opt-out says, each
  one placed under the owning ids the payload names rather than under the group it nests in, and
  every count left exactly as the cross-filtered answer sent it. The standalone Variabelgruppe
  facet's own list is derived beside it, and still leaves out the groups the API opts out of that
  facet whether or not one of them is selected. Both are internal and have no caller on any render
  path, so this version compiles and configures exactly as the last one did and renders the same
  panel bar the Fixed entry below; the panel is rewired onto them in a later change. `KildeLevels`
  and `OnePerId`, which the panel's kilde facet has always used, moved here out of the panel and
  are still called from it, which is why this internal type has four entry points where the tree
  itself needs two. (Fhi.Metadata-raspm)
