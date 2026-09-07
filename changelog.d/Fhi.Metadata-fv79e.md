category: Notes for hosts

- **Give `munin-explorer-drilldown` both tracks of the explorer grid, and exclude it from whatever
  places the rest in the results column.** `Fhi.Helsedata.Stiler` already carries the pair —
  `.munin-explorer > .munin-explorer-drilldown { grid-column: 1/-1; min-inline-size: 0 }` beside a
  catch-all ending `:not(.munin-explorer-drilldown)` — so a host on Stiler is unaffected and owes
  nothing. A host with a stylesheet of its own needs both: the drill-in replaces the list rather
  than sitting beside it, and the filter panel is not rendered at all in that state. Without them
  the open kilde or datasamling view is laid out in the results column beside a 384px filter track
  that is not there, and a long `Gjeldende lovgivning` value then pushes the whole page into
  horizontal scrolling. Measured on the sample host at 1024px: 178px of main column, and 586px with
  the pair in place. The exclusion **alone** is worse than neither — placed nowhere, the drill-in
  auto-flows into the filter track and measures 40px. The sample stylesheets here were missing both.
  (Fhi.Metadata-fv79e)
