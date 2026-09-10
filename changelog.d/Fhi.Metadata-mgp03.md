category: Added

- **The Kilde facet in the variabelutforsker reaches datasamlinger.** The source tree stopped at
  delkilde, which is the level 41 of the catalogue's 44 kilder do not have: 203 datasamlinger hang
  straight off a kilde and none of them could be picked. `FilterOptions` now carries a
  `datasamlinger` facet, and each value hangs under its delkilde where it has one and under its
  kilde where it has none, at any depth. In The Tromsø study that is Tromsø1, Tromsø2 and Tromsø3
  becoming selectable beside the two waves that were already there. The counts are cross-filtered
  like every other value in the panel, a datasamling with no matches is left out as a delkilde
  already is, and the trail step over the results reads a chosen datasamling's name off the facets
  rather than off whichever rows happen to be on screen. Against an API that does not send the
  facet the field is empty and the panel is exactly what it was. A datasamling counts in the
  facet's own search the way a delkilde does — typing its name keeps its kilde — and a chosen
  one draws a chip over the results whether or not that search is showing it.
  (Fhi.Metadata-mgp03)
