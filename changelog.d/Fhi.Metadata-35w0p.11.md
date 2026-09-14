category: Changed
- **The detail pages' fact lists wear the chassis's own class instead of the drill-in panel's.**
  Every `<dl>` the kilde, datasamling and variable views draw is now
  `munin-explorer-page__fields` rather than `munin-explorer-meta__grid`, and the language name over
  a value the catalogue holds in more than one is `munin-explorer-page__language` rather than
  `munin-explorer-meta__language`. Nothing else about the markup changes — the same rows, the same
  `<div>` around each `dt`/`dd` pair, the same count of pairs on every page — and `Fhi.Helsedata.Stiler`
  0.1.75 gives the fact list the panel's numbers unchanged, so on a host that has it the grid and the
  type move nothing. A host carrying its own rule for the panel grid moves by whatever that rule
  says instead; the sample stylesheets here are one such host, and their fact lists gain a 40px row
  gap and a 24px bottom margin. The language marker is the exception on both: Stiler declares
  `margin: 0` for it and none of the panel marker's uppercase, letter-spacing or grey, so the
  language name draws at body size until `Fhi.Metadata-4ozhj` adds them.
  The result row's drill-in panel keeps both of its own names and is untouched: `DetailBlocks` draws
  both surfaces, so the one piece they share takes its class from the caller rather than deciding
  for itself. Until now a detail page borrowed the panel's typography and grid, which is why
  Stiler carried an override putting the panel's two lanes back to one there; neither surface could
  be restyled without the other following it. (Fhi.Metadata-35w0p.11)
