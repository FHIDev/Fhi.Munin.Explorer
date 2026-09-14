category: Changed
- **The detail pages' fact lists wear the chassis's own class instead of the drill-in panel's.**
  Every `<dl>` the kilde, datasamling and variable views draw is now
  `munin-explorer-page__fields` rather than `munin-explorer-meta__grid`, and the language name over
  a value the catalogue holds in more than one is `munin-explorer-page__language` rather than
  `munin-explorer-meta__language`. Nothing else about the markup changes — the same rows, the same
  `<div>` around each `dt`/`dd` pair, the same count of pairs on every page — and `Fhi.Helsedata.Stiler`
  gives the new names the panel's numbers unchanged, so a host that has its rules sees nothing move.
  The result row's drill-in panel keeps both of its own names and is untouched: `DetailBlocks` draws
  both surfaces, so the one piece they share takes its class from the caller rather than deciding
  for itself. Until now a detail page borrowed the panel's typography and grid, which is why
  Stiler carried an override putting the panel's two lanes back to one there; neither surface could
  be restyled without the other following it. (Fhi.Metadata-35w0p.11)
