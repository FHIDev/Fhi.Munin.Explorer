category: Changed
- **The filter panel's source hierarchy is built from the filters answer alone, variabelgrupper
  included.** One pure builder now derives the kilde → delkilde → datasamling → variabelgruppe tree
  from `GET /api/explorer/filters`, so a kilde's groups cost no hierarchy request of their own, and
  every count stays the cross-filtered one the answer sent. The standalone Variabelgruppe facet's
  own list is derived beside it and still leaves out the groups the API opts out of that facet,
  whether or not one of them is selected. Nothing is drawn differently yet — the panel renders the
  tree in a following change. (Fhi.Metadata-raspm)
